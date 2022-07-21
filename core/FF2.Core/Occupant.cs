/*
 * Chess engines have an easy way to Make and Unmake a move.
 * All you need to store is (from-loc, to-loc, captured-piece).
 * But this game does not have that property.
 * Can I think of a clever way to resolve the prespawn cycle without having to clone the grid?
 * Do I even need to?
 * One way to do it would be to model that cycle as many moves. For example
 *   - (destroy occ loc)
 *   - (fall occ from to)
 * Modeling it this way might have other benefits...
 */

using Bits = System.UInt32;

namespace FF2.Core
{
    // The enums in this file get packed into a single value so they must not share any bits.

    public enum OccupantKind : Bits
    {
        None = 0,
        Enemy = 1,
        Catalyst = 2,
        // reserve 3-F for the future
        All = Enemy | Catalyst,
    }

    public enum Color : Bits
    {
        Blank = 0,
        Red = 0x10,
        Yellow = 0x20,
        Blue = Red | Yellow,
        All = Red | Yellow | Blue,
    }

    public enum Direction : Bits
    {
        None = 0,
        Up = 0x40,
        Right = 0x80,
        Down = 0x100,
        Left = 0x200,
        All = Up | Right | Down | Left
    }

    static class EnumHelpers
    {
        const Bits Color1 = (Bits)Color.All;
        const Bits Color0 = Bits.MaxValue ^ Color1;
        const Bits Kind1 = (Bits)OccupantKind.All;
        const Bits Kind0 = Bits.MaxValue ^ Kind1;
        const Bits Direction1 = (Bits)Direction.All;
        const Bits Direction0 = Bits.MaxValue ^ Direction1;

        public static Color GetColor(this Bits val)
        {
            return (Color)(val & Color1);
        }

        public static Bits SetColor(this Bits val, Color color)
        {
            return (val & Color0) | (Bits)color;
        }

        public static OccupantKind GetKind(this Bits val)
        {
            return (OccupantKind)(val & Kind1);
        }

        public static Bits SetKind(this Bits val, OccupantKind kind)
        {
            return (val & Kind0) | (Bits)kind;
        }

        public static Direction GetDirection(this Bits val)
        {
            return (Direction)(val & Direction1);
        }

        public static Bits SetDirection(this Bits val, Direction dir)
        {
            return (val & Direction0) | (Bits)dir;
        }
    }

    public readonly struct Occupant
    {
        private readonly Bits data;

        public static readonly Occupant None = new Occupant();

        public Occupant(OccupantKind kind, Color color, Direction direction)
        {
            this.data = (Bits)kind | (Bits)color | (Bits)direction;
        }

        private Occupant(Bits data)
        {
            this.data = data;
        }

        public Color Color { get { return data.GetColor(); } }

        public Occupant SetColor(Color color)
        {
            return new Occupant(this.data.SetColor(color));
        }

        public OccupantKind Kind { get { return data.GetKind(); } }

        public Direction Direction { get { return data.GetDirection(); } }

        public Occupant SetDirection(Direction direction)
        {
            return new Occupant(this.data.SetDirection(direction));
        }

        public static Occupant MakeCatalyst(Color color, Direction direction)
        {
            return new Occupant(OccupantKind.Catalyst, color, direction);
        }

        public static Occupant MakeEnemy(Color color)
        {
            return new Occupant(OccupantKind.Enemy, color, Direction.None);
        }

        public static bool operator ==(Occupant a, Occupant b)
        {
            return a.data == b.data;
        }

        public static bool operator !=(Occupant a, Occupant b)
        {
            return a.data != b.data;
        }

        public override bool Equals(object? obj)
        {
            return obj is Occupant other && this == other;
        }

        public override int GetHashCode()
        {
            return data.GetHashCode();
        }
    }
}
