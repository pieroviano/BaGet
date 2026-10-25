using System;

namespace BaGet.Identity.Mvc;

/// <summary>
/// The action answers 404 while no mail is configured (the per-action form of <c>AccountControllerBase.RequiresEmail</c>, for a
/// controller whose screens do not all need it).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequiresEmailAttribute : Attribute
{
}

/// <summary>
/// The action needs a signed-in user; a visitor is challenged (the per-action form of
/// <c>AccountControllerBase.RequiresSignIn</c>). Not <c>[Authorize]</c>: while accounts are disabled there is no scheme to
/// challenge with and the screen must answer 404 instead.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequiresSignInAttribute : Attribute
{
}
