namespace HungerAndHavoc.Api
{
    public enum RHAH_PawnRole
    {
        Unspecified = 0,
        Beggar = 1,
        BeggarMother = 2,
        BeggarChild = 3,
        Thief = 4,
        ThiefChild = 5,
        Wild = 6,
        WildChild = 7,
        Trader = 8,
        Escort = 9,
        Mother = 10,
        RatkinYoung = 11,
        Siege = 12,
        Plague = 13,
        Labor = 14,
        Envoy = 15,
        Refugee = 16
    }

    public enum RHAH_Lifecycle
    {
        Arriving = 0,
        SeekingFood = 1,
        Fed = 2,
        Leaving = 3,
        Released = 4,
        Dead = 5
    }

    public enum RHAH_ReleaseReason
    {
        Unspecified = 0,
        Recruited = 1,
        Imprisoned = 2,
        Enslaved = 3,
        JoinedPlayerFaction = 4,
        ModRequest = 5
    }

    public enum RHAH_Attitude
    {
        Hostile = 0,
        LeaningHostile = 1,
        Neutral = 2,
        LeaningFriendly = 3,
        Friendly = 4
    }
}
