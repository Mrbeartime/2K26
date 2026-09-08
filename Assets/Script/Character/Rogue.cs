public class Rogue : Character
{
    public bool LockpickIsAvailable = true;
    public bool OPDoor(RogueDoor door) => door != null && door.UnlockDoor(this);
}

