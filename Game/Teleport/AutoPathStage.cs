namespace OmniToolbox.Teleport;

public enum AutoPathStage
{
    Idle,
    WaitingForLanding,
    WaitingForNavigation,
    Moving,
    Arrived,
    Cancelled,
    Failed,
    Landing
}
