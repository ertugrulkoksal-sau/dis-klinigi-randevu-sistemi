namespace DisKlinigi.Core.Booking.Rules;

/// <summary>
/// Kural 2b: hizmetin bölümü şubede bulunmalı. Hekim hizmeti veriyor olsa bile şube o
/// bölümü barındırmıyorsa (ör. şubede cerrahi ünit yok) randevu verilmiyor.
/// </summary>
public sealed class BranchHostsDepartmentRule : IBookingRule
{
    public RuleResult Check(BookingRequest request, BookingContext context) =>
        request.Branch.HostsDepartment(request.Service.Department)
            ? RuleResult.Passed
            : RuleResult.Violated($"{request.Service.Department.Name} bölümü bu şubede bulunmuyor ({request.Branch.Name}).");
}
