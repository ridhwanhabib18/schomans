namespace SchoMans.Application.Features.GradeBands.ReplaceGradeBands;

/// <param name="Bands">The whole new scale. One band must start at 0 so every final grade gets a label.</param>
public sealed record ReplaceGradeBandsCommand(List<GradeBandInput> Bands);

public sealed record GradeBandInput(string Label, decimal MinScore);
