using Nivtropy.Application.Services;

namespace Nivtropy.Application.DTOs;

public record LevelingMethodOption(
    string Code,
    string Description,
    ToleranceMode Mode,
    double Coefficient,
    double OrientationSign) : IToleranceOption;

public record LevelingClassOption(
    string Code,
    string Description,
    ToleranceMode Mode,
    double Coefficient,
    double ArmDifferenceToleranceStation,
    double ArmDifferenceToleranceAccumulation) : IToleranceOption;
