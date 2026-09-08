namespace GatherUp.Core.Exceptions
{
    public class InvalidInputException : Exception
    {
        public string Field { get; }

        public InvalidInputException(string field, string reason)
            : base($"Invalid value for '{field}': {reason}.")
        {
            Field = field;
        }
    }
}
