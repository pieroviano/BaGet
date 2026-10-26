namespace BaGet.Identity.ViewModels;

/// <summary>
/// A view model that carries the outcome of the last action across a redirect (<c>[TempData] Message</c>). <c>AdminControllerBase.Flash</c> moves the value to and from TempData.
/// </summary>
public interface IFlashMessage
{
    /// <summary>Outcome of the last action.</summary>
    string? Message { get; set; }
}

/// <summary>A <see cref="IFlashMessage"/> that also carries why the last action was refused (<c>[TempData] Error</c>).</summary>
public interface IFlashModel : IFlashMessage
{
    /// <summary>Why the last action was refused.</summary>
    string? Error { get; set; }
}
