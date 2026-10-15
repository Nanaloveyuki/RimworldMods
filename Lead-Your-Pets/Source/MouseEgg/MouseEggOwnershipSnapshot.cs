namespace LeadYourPet
{
    public readonly struct MouseEggOwnershipSnapshot
    {
        public MouseEggOwnershipSnapshot(bool isPet, bool isTravelStock, bool sellAsPrisoner, bool hasCurrentMaster)
        {
            IsPet = isPet;
            IsTravelStock = isTravelStock;
            SellAsPrisoner = sellAsPrisoner;
            HasCurrentMaster = hasCurrentMaster;
        }

        public bool IsPet { get; }
        public bool IsTravelStock { get; }
        public bool SellAsPrisoner { get; }
        public bool HasCurrentMaster { get; }
    }
}
