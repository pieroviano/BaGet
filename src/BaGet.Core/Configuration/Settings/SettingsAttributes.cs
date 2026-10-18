using System;

namespace BaGet.Core;

/// <summary>
/// Marks a setting whose value is a credential: the Settings page shows it masked, never
/// echoes it back, and the service never logs it.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveSettingAttribute : Attribute
{
}

/// <summary>
/// Marks a setting, or every setting of an options type, that is read once at startup
/// (at registration, in the pipeline setup, or through the singleton
/// <c>IOptions&lt;T&gt;</c>), so a change takes effect after a restart. A setting without it
/// is read through <c>IOptionsSnapshot&lt;T&gt;</c> and applies to the next request.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class RestartRequiredAttribute : Attribute
{
}

/// <summary>
/// Keeps a public settable property out of the Settings page: it is internal state that
/// the application sets itself, not something an operator configures.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class HiddenSettingAttribute : Attribute
{
}

/// <summary>
/// An options type bound to a configuration section. Recorded by
/// <see cref="DependencyInjectionExtensions.AddBaGetOptions{TOptions}"/> so that the
/// <see cref="SettingsCatalog"/> lists every options type, provider-specific ones included.
/// </summary>
/// <param name="OptionsType">The options type.</param>
/// <param name="Section">The configuration section it is bound to, or null for the root.</param>
public sealed record SettingsSchemaRegistration(Type OptionsType, string Section);
