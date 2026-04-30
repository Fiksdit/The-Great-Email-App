// FILE: src/GreatEmailApp.Core/Services/Result.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Services;

/// <summary>
/// Discriminated result type for service calls. See rulebook §9.
/// Pattern-match: <c>switch (result) { case Result&lt;T&gt;.Ok ok: ...; case Result&lt;T&gt;.Fail f: ... }</c>
/// </summary>
public abstract record Result<T>
{
    public sealed record Ok(T Value) : Result<T>;
    public sealed record Fail(string Error, Exception? Inner = null) : Result<T>;

    public bool IsOk => this is Ok;
    public T? AsValue => this is Ok ok ? ok.Value : default;
    public string? AsError => this is Fail f ? f.Error : null;
}

public static class Result
{
    public static Result<T>.Ok Ok<T>(T value) => new(value);
    public static Result<T>.Fail Fail<T>(string error, Exception? inner = null) => new(error, inner);
}
