using System;

namespace TicketToRide.Domain
{
    public enum TrainCarColor
    {
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple,
        Black,
        White,
        Locomotive
    }

    public class TrainCarCard : IEquatable<TrainCarCard>
    {
        public TrainCarColor Color { get; }
        public bool IsLocomotive => Color == TrainCarColor.Locomotive;

        public TrainCarCard(TrainCarColor color)
        {
            if (!Enum.IsDefined(typeof(TrainCarColor), color))
            {
                throw new ArgumentOutOfRangeException(nameof(color), "Unsupported train car color.");
            }

            Color = color;
        }

        public bool CanMatch(TrainCarColor routeColor)
        {
            return IsLocomotive || Color == routeColor;
        }

        public override string ToString()
        {
            return IsLocomotive ? "Locomotive" : Color.ToString();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TrainCarCard);
        }

        public bool Equals(TrainCarCard other)
        {
            if (other is null)
            {
                return false;
            }

            return Color == other.Color;
        }

        public override int GetHashCode()
        {
            return Color.GetHashCode();
        }
    }
}
