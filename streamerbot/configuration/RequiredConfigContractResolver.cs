using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Maps configuration properties to camelCase JSON names and
    /// requires every property to have a non-null value.
    /// Use this resolver only for configuration models.
    /// </summary>
    internal sealed class RequiredConfigContractResolver
        : DefaultContractResolver
    {
        public RequiredConfigContractResolver()
        {
            // For example: BaseOddsDenominator -> baseOddsDenominator.
            NamingStrategy = new CamelCaseNamingStrategy();
        }

        protected override JsonProperty CreateProperty(
            MemberInfo member,
            MemberSerialization memberSerialization)
        {
            // Preserve the library's normal property mapping behaviour.
            JsonProperty property = base.CreateProperty(
                member,
                memberSerialization
            );

            // Missing settings must produce an error rather than
            // silently becoming zero or null.
            property.Required = Required.Always;

            return property;
        }
    }
}