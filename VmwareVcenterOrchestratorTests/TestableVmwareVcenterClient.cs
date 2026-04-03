
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

// TestableVmwareVcenterClient.cs
// A thin factory shim that creates a VmwareVcenterClient whose internal HttpClient
// is backed by a custom HttpMessageHandler (e.g. a MockHttpMessageHandler).
//
// The production VmwareVcenterClient hard-constructs its own HttpClientHandler and
// immediately calls /api/session in the constructor. To intercept HTTP without
// modifying production code, we use reflection to:
//   1. Create the client instance without running the constructor body.
//   2. Inject a pre-built HttpClient via its compiler-generated backing field.
//   3. Manually perform the session handshake against the mock and set the auth header.
//
// WHY BACKING FIELDS, NOT PROPERTIES:
//   _logger and VcenterClient are declared as { get; } (getter-only auto-properties).
//   The C# compiler emits a read-only backing field named <PropertyName>k__BackingField
//   for each one. Calling PropertyInfo.SetValue() on a getter-only property throws
//   "Property set method not found" because there is no setter. We must use
//   FieldInfo.SetValue() on the backing field instead.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Keyfactor.Extensions.Orchestrator.VmwareVcenterOrchestrator.Client;
using Keyfactor.Logging;

namespace VmwareVcenterOrchestratorTests;

public static class TestableVmwareVcenterClient
{
    /// <summary>
    /// Creates a <see cref="VmwareVcenterClient"/> whose HTTP calls are routed through
    /// <paramref name="mockHandler"/>. The /api/session POST must be registered on
    /// <paramref name="mockHandler"/> before calling this method.
    /// </summary>
    public static VmwareVcenterClient Create(
        HttpMessageHandler mockHandler,
        string hostname,
        string username,
        string password)
    {
        // 1. Allocate a blank instance — bypasses the real constructor entirely.
        var client = (VmwareVcenterClient)RuntimeHelpers.GetUninitializedObject(typeof(VmwareVcenterClient));

        // 2. Build an HttpClient backed by the mock handler.
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("https://" + hostname)
        };

        // 3. Inject the logger via its compiler-generated backing field.
        //    The property is declared as:  private ILogger _logger { get; }
        //    so the backing field is named <_logger>k__BackingField.
        var loggerField = typeof(VmwareVcenterClient)
            .GetField("<_logger>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "Could not find backing field '<_logger>k__BackingField' on VmwareVcenterClient. " +
                "Call GetFields(NonPublic|Instance) to inspect the actual field names.");
        loggerField.SetValue(client, LogHandler.GetClassLogger<VmwareVcenterClient>());

        // 4. Inject the HttpClient via its compiler-generated backing field.
        //    The property is declared as:  private HttpClient VcenterClient { get; }
        //    so the backing field is named <VcenterClient>k__BackingField.
        var httpClientField = typeof(VmwareVcenterClient)
            .GetField("<VcenterClient>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                "Could not find backing field '<VcenterClient>k__BackingField' on VmwareVcenterClient. " +
                "Call GetFields(NonPublic|Instance) to inspect the actual field names.");
        httpClientField.SetValue(client, httpClient);

        // 5. Perform the session handshake (mirrors what the real constructor does).
        var credentials = username + ":" + password;
        var encodedCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encodedCredentials);

        var response = httpClient.SendAsync(request).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Mock session setup failed: {response.StatusCode}");

        var apiKey = response.Content.ReadAsStringAsync().GetAwaiter().GetResult().Trim('"');
        httpClient.DefaultRequestHeaders.Add("vmware-api-session-id", apiKey);

        return client;
    }
}
