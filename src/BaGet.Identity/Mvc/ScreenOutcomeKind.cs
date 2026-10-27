namespace BaGet.Identity.Mvc;

/// <summary>What a <see cref="ScreenOutcome"/> decided to do.</summary>
internal enum ScreenOutcomeKind { Show, NotFound, Challenge, ChallengeWith, Local, LocalExact, Page, Status }
