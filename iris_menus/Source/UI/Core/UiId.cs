using System;

namespace IrisMenus.UI
{
    public readonly struct UiId : IEquatable<UiId>
    {
        public int Value { get; }
        internal UiId(int value) { Value = value; }
        public bool Equals(UiId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is UiId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(UiId left, UiId right) => left.Equals(right);
        public static bool operator !=(UiId left, UiId right) => !left.Equals(right);
    }

    public readonly struct UiInteraction
    {
        public bool Hovered { get; }
        public bool Pressed { get; }
        public bool Focused { get; }
        public bool Clicked { get; }
        internal UiInteraction(bool hovered, bool pressed, bool focused, bool clicked)
        { Hovered = hovered; Pressed = pressed; Focused = focused; Clicked = clicked; }
    }
}
