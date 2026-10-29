using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OmniToolbox.Config;

internal static class CollectibleConfigJSON
{
    internal static void Validate(Type type)
    {
        if (UsesCollectibleModuleContract(type))
            Validate(type, []);
    }

    internal static void Validate(object value)
    {
        if (!UsesCollectibleModuleContract(value.GetType()))
            return;
        Validate(value.GetType());
        _ = Write(value, new(ReferenceEqualityComparer.Instance), 0);
    }

    internal static JToken Serialize
    (
        object                  value,
        JsonSerializerSettings? settings = null
    )
    {
        var type = value.GetType();
        if (!UsesCollectibleModuleContract(type))
            return settings is null ? JToken.FromObject(value) : JToken.FromObject(value, JsonSerializer.Create(settings));
        if (settings is not null && (settings.Converters.Count > 0 || settings.ContractResolver is not null ||
            settings.NullValueHandling != NullValueHandling.Include || settings.DefaultValueHandling != DefaultValueHandling.Include ||
            settings.TypeNameHandling != TypeNameHandling.None || settings.PreserveReferencesHandling != PreserveReferencesHandling.None ||
            settings.ReferenceLoopHandling != ReferenceLoopHandling.Error || settings.DateTimeZoneHandling != DateTimeZoneHandling.RoundtripKind))
            throw Unsupported(type, "custom converters, resolvers and advanced serializer settings are not supported");
        Validate(type);
        return Write(value, new(ReferenceEqualityComparer.Instance), 0);
    }

    internal static object? Deserialize
    (
        JToken value,
        Type   type
    )
    {
        if (!UsesCollectibleModuleContract(type))
            return value.ToObject(type);
        Validate(type);
        return Read(value, type, 0);
    }

    private static bool UsesCollectibleModuleContract(Type type)
    {
        if (!type.IsCollectible) return false;
        var context = AssemblyLoadContext.GetLoadContext(type.Assembly);
        var hostContext = AssemblyLoadContext.GetLoadContext(typeof(CollectibleConfigJSON).Assembly);
        if (context is { IsCollectible: true } && !ReferenceEquals(context, hostContext)) return true;
        if (type.GetElementType() is { } element && UsesCollectibleModuleContract(element)) return true;
        return type.IsGenericType && type.GenericTypeArguments.Any(UsesCollectibleModuleContract);
    }

    private static void Validate
    (
        Type          type,
        HashSet<Type> visited
    )
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (!visited.Add(type))
            return;
        CheckAttributes(type);
        if (IsScalar(type) || type == typeof(object) || typeof(JToken).IsAssignableFrom(type))
            return;
        if (type.IsPointer || type.IsByRef || typeof(Delegate).IsAssignableFrom(type) || typeof(Type).IsAssignableFrom(type))
            throw Unsupported(type, "the type is not a configuration value");
        if ((TryDictionary(type, out _, out _) || TrySequence(type, out _)) &&
            Attributes(type).Any(static attribute => attribute.AttributeType == typeof(JsonObjectAttribute)))
            throw Unsupported(type, "JsonObject overrides on collection types are not supported");
        if (TryDictionary(type, out var key, out var value))
        {
            if (!IsDictionaryKey(key))
                throw Unsupported(type, "dictionary keys must be strings, enums, integer values or GUIDs");
            Validate(key, visited);
            Validate(value, visited);
            _ = CollectionType(type, value, key);
            return;
        }
        if (TrySequence(type, out var element))
        {
            if (type.IsArray && type.GetArrayRank() != 1)
                throw Unsupported(type, "multidimensional arrays are not supported");
            Validate(element, visited);
            if (!type.IsArray)
                _ = CollectionType(type, element, null);
            return;
        }
        if (type.IsAbstract || type.IsInterface || typeof(System.Runtime.Serialization.ISerializable).IsAssignableFrom(type))
            throw Unsupported(type, "abstract types and custom serialization contracts are not supported");
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            if (method.CustomAttributes.Any(static attribute => attribute.AttributeType.FullName is
                "System.Runtime.Serialization.OnSerializingAttribute" or "System.Runtime.Serialization.OnSerializedAttribute" or
                "System.Runtime.Serialization.OnDeserializingAttribute" or "System.Runtime.Serialization.OnDeserializedAttribute"))
                throw Unsupported(type, "serialization callbacks are not supported");
        var members = Members(type);
        var constructor = Constructor(type);
        foreach (var parameter in constructor?.GetParameters() ?? [])
        {
            CheckAttributes(parameter);
            if (!parameter.HasDefaultValue && !members.Any(member => Matches(member, parameter.Name)))
                throw Unsupported(type, $"constructor parameter {parameter.Name} does not match a serialized member");
            Validate(parameter.ParameterType, visited);
        }
        foreach (var member in members)
            Validate(member.Type, visited);
    }

    private static JToken Write
    (
        object?         value,
        HashSet<object> active,
        int             depth
    )
    {
        CheckDepth(depth);
        if (value is null)
            return JValue.CreateNull();
        var type = value.GetType();
        CheckAttributes(type);
        if (value is JToken token)
            return token.DeepClone();
        if (type.IsEnum)
            return new JValue(Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture));
        if (IsScalar(type))
            return type == typeof(byte[]) ? new JValue(Convert.ToBase64String((byte[])value)) : new JValue(value);
        if (!active.Add(value))
            throw Unsupported(type, "reference loops are not supported");
        try
        {
            Validate(type);
            if (TryDictionary(type, out _, out _))
            {
                var result = new JObject();
                foreach (var pair in (IEnumerable)value)
                {
                    if (pair is null)
                        throw Unsupported(type, "dictionary entries cannot be null");
                    var pairType = pair.GetType();
                    var keyMember = pairType.GetProperty("Key")
                        ?? throw Unsupported(type, "dictionary entries must expose Key");
                    var valueMember = pairType.GetProperty("Value")
                        ?? throw Unsupported(type, "dictionary entries must expose Value");
                    var key = keyMember.GetValue(pair)
                        ?? throw Unsupported(type, "dictionary keys cannot be null");
                    var name = Convert.ToString(key, CultureInfo.InvariantCulture)
                        ?? throw Unsupported(type, "dictionary keys must have a string representation");
                    if (result.ContainsKey(name))
                        throw Unsupported(type, $"duplicate dictionary key {name}");
                    result[name] = Write(valueMember.GetValue(pair), active, depth + 1);
                }
                return result;
            }
            if (TrySequence(type, out _))
            {
                var result = new JArray();
                foreach (var item in (IEnumerable)value)
                    result.Add(Write(item, active, depth + 1));
                return result;
            }
            var json = new JObject();
            foreach (var member in Members(type))
                json[member.Name] = Write(member.Get(value), active, depth + 1);
            return json;
        }
        finally
        {
            active.Remove(value);
        }
    }

    private static object? Read
    (
        JToken token,
        Type   type,
        int    depth
    )
    {
        CheckDepth(depth);
        var underlying = Nullable.GetUnderlyingType(type);
        if (token.Type is JTokenType.Null or JTokenType.Undefined)
        {
            if (type.IsValueType && underlying is null)
                throw Unsupported(type, "null cannot be assigned to a value type");
            return null;
        }
        type = underlying ?? type;
        if (typeof(JToken).IsAssignableFrom(type) || type == typeof(object))
            return token is JValue scalar && type == typeof(object) ? scalar.Value : token.DeepClone();
        if (type.IsEnum)
        {
            if (token.Type == JTokenType.String)
            {
                var name = token.Value<string>() ?? throw Unsupported(type, "expected an enum name");
                return Enum.Parse(type, name, true);
            }
            var numeric = token.ToObject(Enum.GetUnderlyingType(type)) ?? throw Unsupported(type, "expected an enum value");
            return Enum.ToObject(type, numeric);
        }
        if (IsScalar(type))
            return token.ToObject(type);
        if (TryDictionary(type, out var keyType, out var valueType))
        {
            if (token is not JObject json)
                throw Unsupported(type, "expected a JSON object");
            var concrete = CollectionType(type, valueType, keyType);
            var dictionary = Create(concrete);
            var add = concrete.GetMethod("Add", [keyType, valueType])
                ?? throw Unsupported(type, "the dictionary has no public Add method");
            foreach (var property in json.Properties())
                add.Invoke(dictionary, [ReadKey(property.Name, keyType), Read(property.Value, valueType, depth + 1)]);
            return dictionary;
        }
        if (TrySequence(type, out var elementType))
        {
            if (token is not JArray array)
                throw Unsupported(type, "expected a JSON array");
            if (type.IsArray)
            {
                var result = Array.CreateInstance(elementType, array.Count);
                for (var index = 0; index < array.Count; index++)
                    result.SetValue(Read(array[index], elementType, depth + 1), index);
                return result;
            }
            var concrete = CollectionType(type, elementType, null);
            var collection = Create(concrete);
            var add = concrete.GetMethod("Add", [elementType])
                ?? throw Unsupported(type, "the collection has no public Add method");
            foreach (var item in array)
                add.Invoke(collection, [Read(item, elementType, depth + 1)]);
            return collection;
        }
        if (token is not JObject root)
            throw Unsupported(type, "expected a JSON object");
        var members = Members(type);
        var constructor = Constructor(type);
        var parameters = constructor?.GetParameters() ?? [];
        var arguments = parameters.Select(parameter =>
        {
            var member = members.FirstOrDefault(member => Matches(member, parameter.Name));
            return member is not null && root.TryGetValue(member.Name, StringComparison.OrdinalIgnoreCase, out var value)
                ? Read(value, parameter.ParameterType, depth + 1)
                : parameter.HasDefaultValue ? parameter.DefaultValue : Default(parameter.ParameterType);
        }).ToArray();
        var instance = constructor is null ? Create(type) : constructor.Invoke(arguments);
        foreach (var member in members)
        {
            if (!member.Writable || parameters.Any(parameter => Matches(member, parameter.Name)))
                continue;
            if (root.TryGetValue(member.Name, StringComparison.OrdinalIgnoreCase, out var value))
                member.Set(instance, Read(value, member.Type, depth + 1));
        }
        return instance;
    }

    private static Member[] Members(Type type)
    {
        var optIn = false;
        var attribute = Attributes(type).FirstOrDefault(static attribute => attribute.AttributeType == typeof(JsonObjectAttribute));
        if (attribute is not null)
        {
            var mode = attribute.ConstructorArguments.FirstOrDefault(argument => argument.ArgumentType == typeof(MemberSerialization));
            var named = attribute.NamedArguments.FirstOrDefault(static argument => argument.MemberName == nameof(JsonObjectAttribute.MemberSerialization));
            var value = named.MemberInfo is not null ? named.TypedValue.Value : mode.Value;
            if (value is not null)
            {
                var serialization = (MemberSerialization)Convert.ToInt32(value, CultureInfo.InvariantCulture);
                if (serialization == MemberSerialization.Fields)
                    throw Unsupported(type, "JsonObject MemberSerialization.Fields is not supported");
                optIn = serialization == MemberSerialization.OptIn;
            }
        }
        List<Member> result = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        HashSet<string> clrNames = new(StringComparer.Ordinal);
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var member in current.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (member is not FieldInfo && member is not PropertyInfo)
                    continue;
                var attributes = Attributes(member).ToArray();
                if (attributes.Any(static attribute => attribute.AttributeType == typeof(JsonIgnoreAttribute) ||
                    attribute.AttributeType.FullName == "System.NonSerializedAttribute"))
                {
                    clrNames.Add(member.Name);
                    continue;
                }
                CheckAttributes(member);
                var propertyAttribute = attributes.FirstOrDefault(static attribute => attribute.AttributeType == typeof(JsonPropertyAttribute));
                var annotated = propertyAttribute is not null;
                Type memberType;
                bool readable;
                bool writable;
                if (member is FieldInfo field)
                {
                    readable   = field.IsPublic || annotated;
                    writable   = !field.IsInitOnly;
                    memberType = field.FieldType;
                }
                else
                {
                    var property = (PropertyInfo)member;
                    if (property.GetIndexParameters().Length != 0)
                        continue;
                    readable   = property.GetMethod is { } getter && (getter.IsPublic || annotated);
                    writable   = property.SetMethod is { } setter && (setter.IsPublic || annotated);
                    memberType = property.PropertyType;
                }
                if (!readable || optIn && !annotated)
                    continue;
                if (!clrNames.Add(member.Name))
                    continue;
                var name = propertyAttribute?.ConstructorArguments.FirstOrDefault().Value as string;
                var namedName = propertyAttribute?.NamedArguments.FirstOrDefault(static argument => argument.MemberName == nameof(JsonPropertyAttribute.PropertyName));
                name = namedName?.TypedValue.Value as string ?? name ?? member.Name;
                if (!names.Add(name))
                    throw Unsupported(type, $"duplicate JSON member {name}");
                result.Add(new(member, name, memberType, writable));
            }
        }
        return [.. result];
    }

    private static ConstructorInfo? Constructor(Type type)
    {
        if (type.IsValueType)
            return null;
        var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var marked = constructors.Where(static constructor => constructor.CustomAttributes.Any(static attribute =>
            attribute.AttributeType == typeof(JsonConstructorAttribute))).ToArray();
        if (marked.Length == 1)
            return marked[0];
        if (marked.Length > 1)
            throw Unsupported(type, "multiple JsonConstructor constructors were declared");
        var parameterless = constructors.FirstOrDefault(static constructor => constructor.IsPublic && constructor.GetParameters().Length == 0);
        if (parameterless is not null)
            return parameterless;
        var publicConstructors = constructors.Where(static constructor => constructor.IsPublic).ToArray();
        if (publicConstructors.Length == 1)
            return publicConstructors[0];
        return constructors.FirstOrDefault(static constructor => constructor.GetParameters().Length == 0)
            ?? throw Unsupported(type, "a parameterless or single public constructor is required");
    }

    private static void CheckAttributes(ICustomAttributeProvider provider)
    {
        foreach (var attribute in Attributes(provider))
        {
            var name = attribute.AttributeType.FullName ?? string.Empty;
            if (name.StartsWith("System.Runtime.Serialization.", StringComparison.Ordinal) &&
                name is "System.Runtime.Serialization.DataContractAttribute" or "System.Runtime.Serialization.DataMemberAttribute" or
                    "System.Runtime.Serialization.IgnoreDataMemberAttribute")
                throw new NotSupportedException($"Collectible module configuration {provider}: {name} is not supported.");
            if (!name.StartsWith("Newtonsoft.Json.", StringComparison.Ordinal))
                continue;
            if (attribute.AttributeType != typeof(JsonIgnoreAttribute) && attribute.AttributeType != typeof(JsonConstructorAttribute) &&
                attribute.AttributeType != typeof(JsonPropertyAttribute) && attribute.AttributeType != typeof(JsonObjectAttribute))
                throw new NotSupportedException($"Collectible module configuration {provider}: {name} is not supported.");
            var allowed = attribute.AttributeType == typeof(JsonPropertyAttribute) ? nameof(JsonPropertyAttribute.PropertyName)
                : attribute.AttributeType == typeof(JsonObjectAttribute) ? nameof(JsonObjectAttribute.MemberSerialization) : null;
            if (attribute.NamedArguments.Any(argument => argument.MemberName != allowed))
                throw new NotSupportedException($"Collectible module configuration {provider}: advanced {attribute.AttributeType.Name} options are not supported.");
            if (provider is ParameterInfo && attribute.AttributeType != typeof(JsonIgnoreAttribute))
                throw new NotSupportedException($"Collectible module configuration {provider}: constructor parameter JSON attributes are not supported.");
        }
    }

    private static IEnumerable<CustomAttributeData> Attributes(ICustomAttributeProvider provider)
    {
        if (provider is Type type)
        {
            for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
                foreach (var attribute in current.CustomAttributes)
                    yield return attribute;
            yield break;
        }
        if (provider is ParameterInfo parameter)
        {
            foreach (var attribute in parameter.CustomAttributes)
                yield return attribute;
            yield break;
        }
        if (provider is not MemberInfo member)
            yield break;
        var direct = member.CustomAttributes.ToArray();
        foreach (var attribute in direct)
            yield return attribute;
        if (member is PropertyInfo property && property.GetMethod is { } getter &&
            getter.GetBaseDefinition() is { } definition && definition.DeclaringType != getter.DeclaringType &&
            definition.DeclaringType?.GetProperty(property.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is { } inherited)
            foreach (var attribute in inherited.CustomAttributes)
                if (!direct.Any(item => item.AttributeType == attribute.AttributeType))
                    yield return attribute;
    }

    private static bool TryDictionary
    (
        Type     type,
        out Type key,
        out Type value
    )
    {
        var contract = type.GetInterfaces().Prepend(type).FirstOrDefault(static item => item.IsGenericType &&
            (item.GetGenericTypeDefinition() == typeof(IDictionary<,>) || item.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
        var arguments = contract?.GetGenericArguments();
        key   = arguments?[0] ?? typeof(object);
        value = arguments?[1] ?? typeof(object);
        return contract is not null;
    }

    private static bool TrySequence
    (
        Type     type,
        out Type element
    )
    {
        if (type.IsArray)
        {
            element = type.GetElementType() ?? throw Unsupported(type, "the array has no element type");
            return true;
        }
        var contract = type.GetInterfaces().Prepend(type).FirstOrDefault(static item => item.IsGenericType &&
            item.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        element = contract?.GetGenericArguments()[0] ?? typeof(object);
        return contract is not null;
    }

    private static Type CollectionType
    (
        Type  type,
        Type  element,
        Type? key
    )
    {
        var concrete = type;
        if (type.IsInterface)
        {
            concrete = key is not null ? typeof(Dictionary<,>).MakeGenericType(key, element)
                : type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(ISet<>) || type.GetGenericTypeDefinition() == typeof(IReadOnlySet<>))
                    ? typeof(HashSet<>).MakeGenericType(element) : typeof(List<>).MakeGenericType(element);
            if (!type.IsAssignableFrom(concrete))
                throw Unsupported(type, "the collection interface has no supported concrete representation");
        }
        if (concrete.IsAbstract || concrete.GetConstructor(Type.EmptyTypes) is null ||
            concrete.GetMethod("Add", key is null ? [element] : [key, element]) is null)
            throw Unsupported(type, "collections require a public parameterless constructor and Add method");
        return concrete;
    }

    private static bool IsScalar(Type type) => type.IsEnum || type.IsPrimitive || type == typeof(string) ||
        type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) ||
        type == typeof(TimeSpan) || type == typeof(Uri) || type == typeof(byte[]);

    private static bool IsDictionaryKey(Type type) => type == typeof(string) || type.IsEnum || type == typeof(Guid) ||
        Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or
            TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64;

    private static object ReadKey
    (
        string text,
        Type   type
    ) => type.IsEnum ? Enum.Parse(type, text, true)
        : type == typeof(Guid) ? Guid.Parse(text) : Convert.ChangeType(text, type, CultureInfo.InvariantCulture);

    private static bool Matches
    (
        Member  member,
        string? name
    ) => string.Equals(member.Info.Name, name, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase);

    private static object? Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    private static object Create(Type type) => Activator.CreateInstance(type)
        ?? throw Unsupported(type, "the constructor returned no instance");

    private static void CheckDepth(int depth)
    {
        if (depth > 64)
            throw new NotSupportedException("Collectible module configuration exceeds the maximum JSON depth of 64.");
    }

    private static NotSupportedException Unsupported
    (
        Type   type,
        string reason
    ) =>
        new($"Collectible module configuration {type.FullName}: {reason}.");

    private sealed record Member
    (
        MemberInfo Info,
        string     Name,
        Type       Type,
        bool       Writable
    )
    {
        internal object? Get(object instance) => Info is FieldInfo field ? field.GetValue(instance) : ((PropertyInfo)Info).GetValue(instance);

        internal void Set
        (
            object  instance,
            object? value
        )
        {
            if (Info is FieldInfo field)
                field.SetValue(instance, value);
            else
                ((PropertyInfo)Info).SetValue(instance, value);
        }
    }
}
