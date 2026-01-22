using Nivtropy.Application.Services;
using Nivtropy.Domain.Model;

namespace Nivtropy.Tests;

public class ImportValidationServiceTests
{
    [Fact]
    public void StationCounter_CountsStationFromSingleRecord()
    {
        var counter = new ImportValidationService.StationCounter();
        var record = new MeasurementRecord { Rb_m = 1.234, Rf_m = 1.111 };

        counter.TryAddStation(record, out _);

        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public void StationCounter_CountsStationAcrossRecordsInSameRun()
    {
        var counter = new ImportValidationService.StationCounter();
        var backRecord = new MeasurementRecord { Rb_m = 1.234 };
        var foreRecord = new MeasurementRecord { Rf_m = 1.111 };

        counter.TryAddStation(backRecord, out _);
        counter.TryAddStation(foreRecord, out _);

        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public void StationCounter_DoesNotCombineAcrossRunBoundary()
    {
        var counter = new ImportValidationService.StationCounter();
        var backRecord = new MeasurementRecord { Rb_m = 1.234 };
        var foreRecord = new MeasurementRecord { Rf_m = 1.111 };

        counter.TryAddStation(backRecord, out _);
        counter.ResetPending();
        counter.TryAddStation(foreRecord, out _);

        Assert.Equal(0, counter.Count);
    }
}
