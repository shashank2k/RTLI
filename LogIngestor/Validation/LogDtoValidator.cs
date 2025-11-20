using System;
using System.Linq;
using FluentValidation;
using LogIngestor.Models;

namespace LogIngestor.Validation;

public class LogDtoValidator : AbstractValidator<LogDto>
{
// Allow common level names, case-insensitive
private static readonly string[] Levels = new[]
{
"Verbose", "Debug", "Information", "Warning", "Error", "Fatal", "Trace"
};public LogDtoValidator()
{
    // Timestamp must not be empty and should be close to now (tolerate slight clock skew)
    RuleFor(x => x.Timestamp)
        .NotEmpty().WithMessage("timestamp is required")
        .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMinutes(5)).WithMessage("timestamp is in the future");

    // Level required, normalized, and within allowed set (case-insensitive)
    RuleFor(x => x.Level)
        .NotEmpty().WithMessage("level is required")
        .Must(v => Levels.Any(l => string.Equals(l, v, StringComparison.OrdinalIgnoreCase)))
        .WithMessage($"level must be one of: {string.Join(", ", Levels)}");

    // Message required and size-limited
    RuleFor(x => x.Message)
        .NotEmpty().WithMessage("message is required")
        .MaximumLength(2048).WithMessage("message too long (max 2048 chars)");

    // App required and size-limited; allow letters, digits, dash, underscore, dot
    RuleFor(x => x.App)
        .NotEmpty().WithMessage("app is required")
        .MaximumLength(128).WithMessage("app too long (max 128 chars)")
        .Matches(@"^[A-Za-z0-9._-]+$").WithMessage("app must contain only letters, digits, dot, dash, underscore");

    // Optional fields with length limits
    RuleFor(x => x.UserId)
        .MaximumLength(128).When(x => !string.IsNullOrEmpty(x.UserId)).WithMessage("userId too long (max 128 chars)");

    RuleFor(x => x.TraceId)
        .MaximumLength(128).When(x => !string.IsNullOrEmpty(x.TraceId)).WithMessage("traceId too long (max 128 chars)");

    // Context: any JSON serializable object; size enforced at request gate (e.g., 64KB limit)
    // No rule needed; keep as-is for flexibility
}
}