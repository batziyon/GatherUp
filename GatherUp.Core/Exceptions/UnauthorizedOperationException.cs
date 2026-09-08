namespace GatherUp.Core.Exceptions
{
    public class UnauthorizedOperationException : Exception
    {
        public int    UserId    { get; }
        public string Operation { get; }

        public UnauthorizedOperationException(int userId, string operation)
            : base($"User {userId} is not authorized to perform '{operation}'.")
        {
            UserId    = userId;
            Operation = operation;
        }
    }
}
