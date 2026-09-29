namespace UnityIsekaiGame.Economy.Payroll
{
    public enum CompensationCategory
    {
        HourlyWage = 0,
        DailyWage = 100,
        ShiftWage = 200,
        PieceRate = 300,
        TaskRate = 400,
        WeeklySalary = 500,
        MonthlySalary = 600,
        AnnualSalary = 700,
        Stipend = 800,
        ApprenticeshipWage = 900,
        HazardPayFoundation = 1000,
        Custom = 9000
    }

    public enum CompensationRateBasis
    {
        PerDurationUnit = 0,
        PerShift = 100,
        PerTask = 200,
        PerOutputQuantity = 300,
        PerPayPeriod = 400
    }

    public enum PayrollDurationUnit
    {
        Minute = 0,
        Hour = 100,
        Day = 200,
        Shift = 300,
        Task = 400,
        OutputUnit = 500,
        PayPeriod = 600
    }

    public enum PayScheduleKind
    {
        PerShift = 0,
        Daily = 100,
        Weekly = 200,
        FixedDays = 300,
        MonthlyFoundation = 400,
        PerCompletedTask = 500,
        PerOutputBatch = 600,
        OnDemandFoundation = 700,
        Custom = 9000
    }

    public enum PayrollRoundingMode
    {
        TowardZero = 0,
        NearestUp = 100,
        HalfAwayFromZero = 200,
        Ceiling = 300,
        Floor = 400
    }

    public enum CompensationAgreementState
    {
        Draft = 0,
        Active = 100,
        Suspended = 200,
        Ended = 300,
        Superseded = 400,
        Cancelled = 500
    }

    public enum WorkScheduleCategory
    {
        Unscheduled = 0,
        FixedShift = 100,
        FlexibleHours = 200,
        TaskBased = 300,
        OutputBased = 400,
        OnCallFoundation = 500
    }

    public enum WorkClassification
    {
        Regular = 0,
        Overtime = 100,
        Hazard = 200,
        Emergency = 300,
        Training = 400,
        Apprentice = 500
    }

    public enum TimesheetState
    {
        Draft = 0,
        Submitted = 100,
        Approved = 200,
        Rejected = 300,
        Superseded = 400
    }

    public enum PayPeriodState
    {
        Open = 0,
        Calculated = 100,
        Obligated = 200,
        Paid = 300,
        PartiallyPaid = 400,
        Closed = 500,
        Corrected = 600
    }

    public enum CompensationAdjustmentCategory
    {
        Bonus = 0,
        Premium = 100,
        Reimbursement = 200,
        Allowance = 300,
        Correction = 400,
        Penalty = 500
    }

    public enum DeductionCategory
    {
        Tax = 0,
        Fee = 100,
        Garnishment = 200,
        Benefit = 300,
        Repayment = 400,
        Custom = 9000
    }

    public enum DeductionCalculationBase
    {
        GrossWages = 0,
        GrossIncludingReimbursements = 100,
        NetAfterEarlierDeductions = 200,
        FixedOnly = 300
    }

    public enum DeductionInsufficientGrossPolicy
    {
        CapAtAvailable = 0,
        RejectCalculation = 100,
        CarryForwardDebt = 200
    }

    public enum PayrollObligationState
    {
        Pending = 0,
        Reserved = 100,
        Paid = 200,
        PartiallyPaid = 300,
        DebtOutstanding = 400,
        Cancelled = 500,
        Corrected = 600
    }

    public enum PayrollRunState
    {
        Draft = 0,
        FundsReserved = 100,
        Executed = 200,
        PartiallyExecuted = 300,
        FailedRolledBack = 400,
        Cancelled = 500
    }

    public enum PayrollPaymentPolicy
    {
        AllOrNothing = 0,
        PartialWithDebt = 100
    }

    public enum PayrollOperationCode
    {
        Succeeded = 0,
        Preview = 100,
        Duplicate = 200,
        InvalidRequest = 300,
        MissingDefinition = 400,
        MissingEmployment = 500,
        MissingAccount = 600,
        CurrencyMismatch = 700,
        AgreementOverlap = 800,
        InvalidState = 900,
        InsufficientFunds = 1000,
        CalculationRejected = 1100,
        PersistenceRejected = 1200,
        RolledBack = 1300,
        AccessDenied = 1400
    }

    public enum PayrollProjectionAudience
    {
        Employee = 0,
        Employer = 100,
        PayrollAuthority = 200,
        Public = 300,
        PrivilegedDebug = 400
    }
}
