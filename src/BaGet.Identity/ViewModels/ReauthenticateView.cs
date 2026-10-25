using System;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>What the "sign in again" prompt shows.</summary>
public record ReauthenticateView(bool Done, bool Available, string ReturnUrl);
