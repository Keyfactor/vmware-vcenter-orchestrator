using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace VmwareVcenterOrchestrator
{
    internal class PamUtilities
    {
        internal static string ResolvePAMField(IPAMSecretResolver resolver, ILogger logger, string name, string key)
        {
            if (resolver == null) return key;
            else
            {
                return resolver.Resolve(key);
            }
        }
    }
}
