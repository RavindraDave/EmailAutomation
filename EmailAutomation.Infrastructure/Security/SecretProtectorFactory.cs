using System;
using EmailAutomation.Application.Services;

namespace EmailAutomation.Infrastructure.Security;

public static class SecretProtectorFactory
{
    public static ISecretProtector Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsDpapiSecretProtector();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsKeychainSecretProtector();
        }

        return new InsecureFallbackSecretProtector();
    }
}
