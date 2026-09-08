using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using GatherUp.Core.Interfaces;

namespace GatherUp.Core.DO
{
    public abstract class Person : IEntity
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        public required string Name     { get; set; } = string.Empty;
        public required string Email    { get; set; } = string.Empty;
        public string          Password { get; set; } = string.Empty;

        [SetsRequiredMembers]
        protected Person() { }
    }
}
