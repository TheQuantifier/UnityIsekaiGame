using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy.Payroll
{
    [Serializable]
    public sealed class PayrollRationalData
    {
        public long numerator = 0L;
        public long denominator = 1L;

        public PayrollRationalData Clone()
        {
            return new PayrollRationalData
            {
                numerator = numerator,
                denominator = denominator <= 0L ? 1L : denominator
            };
        }

        public bool IsPositive => numerator > 0L && denominator > 0L;
    }

    [CreateAssetMenu(fileName = "CompensationDefinition", menuName = "Unity Isekai Game/Economy/Compensation Definition")]
    public class CompensationDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string compensationDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private CompensationCategory category = CompensationCategory.HourlyWage;
        [SerializeField] private CurrencyDefinition currency;
        [SerializeField] private CompensationRateBasis rateBasis = CompensationRateBasis.PerDurationUnit;
        [SerializeField, Min(1)] private long rateUnits = 1L;
        [SerializeField, Min(1)] private long quantityUnit = 1L;
        [SerializeField] private PayrollDurationUnit durationUnit = PayrollDurationUnit.Hour;
        [SerializeField] private PayScheduleKind scheduleKind = PayScheduleKind.Weekly;
        [SerializeField] private PayrollRoundingMode roundingMode = PayrollRoundingMode.HalfAwayFromZero;
        [SerializeField, Min(1)] private int definitionVersion = 1;
        [SerializeField] private string accessPolicyId;

        public string Id => compensationDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public CompensationCategory Category => category;
        public CurrencyDefinition Currency => currency;
        public string CurrencyId => currency == null ? string.Empty : currency.Id;
        public CompensationRateBasis RateBasis => rateBasis;
        public long RateUnits => Math.Max(1L, rateUnits);
        public long QuantityUnit => Math.Max(1L, quantityUnit);
        public PayrollDurationUnit DurationUnit => durationUnit;
        public PayScheduleKind ScheduleKind => scheduleKind;
        public PayrollRoundingMode RoundingMode => roundingMode;
        public int DefinitionVersion => Math.Max(1, definitionVersion);
        public string AccessPolicyId => accessPolicyId ?? string.Empty;

        public void Initialize(string id, string display, CurrencyDefinition currencyDefinition, CompensationCategory compensationCategory, CompensationRateBasis basis, long units, long quantity = 1L, PayrollDurationUnit duration = PayrollDurationUnit.Hour, PayScheduleKind schedule = PayScheduleKind.Weekly, PayrollRoundingMode rounding = PayrollRoundingMode.HalfAwayFromZero)
        {
            compensationDefinitionId = id ?? string.Empty;
            displayName = display ?? string.Empty;
            currency = currencyDefinition;
            category = compensationCategory;
            rateBasis = basis;
            rateUnits = Math.Max(1L, units);
            quantityUnit = Math.Max(1L, quantity);
            durationUnit = duration;
            scheduleKind = schedule;
            roundingMode = rounding;
            definitionVersion = Math.Max(1, definitionVersion);
        }

        private void OnValidate()
        {
            rateUnits = Math.Max(1L, rateUnits);
            quantityUnit = Math.Max(1L, quantityUnit);
            definitionVersion = Math.Max(1, definitionVersion);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("compensation.", StringComparison.Ordinal))
            {
                report.AddError($"Compensation definition '{DisplayName}' must use the 'compensation.' namespace.");
            }

            if (currency == null || definitionsById == null || !definitionsById.TryGetValue(currency.Id, out IGameDefinition foundCurrency) || foundCurrency is not CurrencyDefinition)
            {
                report.AddError($"Compensation definition '{DisplayName}' must reference a catalog Currency definition.");
            }

            if (!Enum.IsDefined(typeof(CompensationCategory), category)
                || !Enum.IsDefined(typeof(CompensationRateBasis), rateBasis)
                || !Enum.IsDefined(typeof(PayrollDurationUnit), durationUnit)
                || !Enum.IsDefined(typeof(PayScheduleKind), scheduleKind)
                || !Enum.IsDefined(typeof(PayrollRoundingMode), roundingMode))
            {
                report.AddError($"Compensation definition '{DisplayName}' has an invalid enum configuration.");
            }

            if (RateUnits <= 0L || QuantityUnit <= 0L || DefinitionVersion <= 0)
            {
                report.AddError($"Compensation definition '{DisplayName}' has an invalid rate, quantity, or version.");
            }
        }
    }

    [CreateAssetMenu(fileName = "PayrollDeductionDefinition", menuName = "Unity Isekai Game/Economy/Payroll Deduction Definition")]
    public sealed class PayrollDeductionDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string deductionDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private DeductionCategory category = DeductionCategory.Tax;
        [SerializeField] private CurrencyDefinition currency;
        [SerializeField, Min(0)] private long fixedAmountUnits;
        [SerializeField] private PayrollRationalData ratio = new PayrollRationalData();
        [SerializeField] private DeductionCalculationBase calculationBase = DeductionCalculationBase.GrossWages;
        [SerializeField] private DeductionInsufficientGrossPolicy insufficientGrossPolicy = DeductionInsufficientGrossPolicy.CapAtAvailable;
        [SerializeField] private PayrollRoundingMode roundingMode = PayrollRoundingMode.HalfAwayFromZero;
        [SerializeField, Min(0)] private int priority;
        [SerializeField] private string recipientAccountId;
        [SerializeField] private bool authorizationRequired;
        [SerializeField, Min(1)] private int definitionVersion = 1;
        [SerializeField] private string accessPolicyId;

        public string Id => deductionDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public DeductionCategory Category => category;
        public CurrencyDefinition Currency => currency;
        public string CurrencyId => currency == null ? string.Empty : currency.Id;
        public long FixedAmountUnits => Math.Max(0L, fixedAmountUnits);
        public PayrollRationalData Ratio => ratio?.Clone() ?? new PayrollRationalData();
        public DeductionCalculationBase CalculationBase => calculationBase;
        public DeductionInsufficientGrossPolicy InsufficientGrossPolicy => insufficientGrossPolicy;
        public PayrollRoundingMode RoundingMode => roundingMode;
        public int Priority => Math.Max(0, priority);
        public string RecipientAccountId => recipientAccountId ?? string.Empty;
        public bool AuthorizationRequired => authorizationRequired;
        public int DefinitionVersion => Math.Max(1, definitionVersion);
        public string AccessPolicyId => accessPolicyId ?? string.Empty;

        public void Initialize(string id, string display, CurrencyDefinition currencyDefinition, DeductionCategory deductionCategory, long fixedUnits, PayrollRationalData proportionalRatio, int order, string recipientAccount = "", DeductionCalculationBase basis = DeductionCalculationBase.GrossWages, DeductionInsufficientGrossPolicy insufficientPolicy = DeductionInsufficientGrossPolicy.CapAtAvailable)
        {
            deductionDefinitionId = id ?? string.Empty;
            displayName = display ?? string.Empty;
            currency = currencyDefinition;
            category = deductionCategory;
            fixedAmountUnits = Math.Max(0L, fixedUnits);
            ratio = proportionalRatio?.Clone() ?? new PayrollRationalData();
            priority = Math.Max(0, order);
            recipientAccountId = recipientAccount ?? string.Empty;
            calculationBase = basis;
            insufficientGrossPolicy = insufficientPolicy;
            definitionVersion = Math.Max(1, definitionVersion);
        }

        private void OnValidate()
        {
            fixedAmountUnits = Math.Max(0L, fixedAmountUnits);
            priority = Math.Max(0, priority);
            definitionVersion = Math.Max(1, definitionVersion);
            ratio ??= new PayrollRationalData();
            ratio.denominator = Math.Max(1L, ratio.denominator);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("payroll-deduction.", StringComparison.Ordinal))
            {
                report.AddError($"Payroll Deduction definition '{DisplayName}' must use the 'payroll-deduction.' namespace.");
            }

            if (currency == null || definitionsById == null || !definitionsById.TryGetValue(currency.Id, out IGameDefinition foundCurrency) || foundCurrency is not CurrencyDefinition)
            {
                report.AddError($"Payroll Deduction definition '{DisplayName}' must reference a catalog Currency definition.");
            }

            if (FixedAmountUnits <= 0L && !Ratio.IsPositive)
            {
                report.AddError($"Payroll Deduction definition '{DisplayName}' must declare a fixed amount or positive ratio.");
            }

            if (!Enum.IsDefined(typeof(DeductionCategory), category)
                || !Enum.IsDefined(typeof(DeductionCalculationBase), calculationBase)
                || !Enum.IsDefined(typeof(DeductionInsufficientGrossPolicy), insufficientGrossPolicy)
                || !Enum.IsDefined(typeof(PayrollRoundingMode), roundingMode))
            {
                report.AddError($"Payroll Deduction definition '{DisplayName}' has an invalid enum configuration.");
            }
        }
    }
}
