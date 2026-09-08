namespace GatherUp.Core.Exceptions
{
    public class EntityNotFoundException : Exception
    {
        public string EntityType { get; }
        public int    EntityId   { get; }

        public EntityNotFoundException(string entityType, int id)
            : base($"{entityType} with Id={id} was not found.")
        {
            EntityType = entityType;
            EntityId   = id;
        }
    }
}
