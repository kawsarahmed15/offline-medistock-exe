using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Medistock.Domain.Common;

public sealed class Money : ValueObject, IComparable<Money>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public static readonly Money Zero = new(0m);

    public Money(decimal amount, string currency = "INR")
    {
        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency ?? "INR";
    }

    public static Money From(decimal amount, string currency = "INR") => new(amount, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier) =>
        new(money.Amount * multiplier, money.Currency);

    public static Money operator *(decimal multiplier, Money money) => money * multiplier;

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0) throw new DivideByZeroException("Cannot divide money by zero.");
        return new Money(money.Amount / divisor, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Cannot operate on different currencies ({left.Currency} vs {right.Currency}).");
    }

    public int CompareTo(Money? other)
    {
        if (other is null) return 1;
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => $"{Currency} {Amount:N2}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency.ToUpperInvariant();
    }
}

public sealed class Gstin : ValueObject
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled);

    public string Value { get; }
    public string StateCode => Value.Length >= 2 ? Value.Substring(0, 2) : string.Empty;

    public Gstin(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("GSTIN cannot be empty.", nameof(value));

        var clean = value.Trim().ToUpperInvariant();
        if (!GstinRegex.IsMatch(clean))
            throw new ArgumentException($"Invalid GSTIN format: '{value}'. Must follow 15-character statutory format.", nameof(value));

        Value = clean;
    }

    public static bool TryParse(string? value, out Gstin? gstin)
    {
        gstin = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var clean = value.Trim().ToUpperInvariant();
        if (!GstinRegex.IsMatch(clean)) return false;
        gstin = new Gstin(clean);
        return true;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

public sealed class PackSize : ValueObject
{
    public int UnitsPerPack { get; }
    public string BaseUnit { get; } // e.g. TAB, CAP, ML, GM

    public PackSize(int unitsPerPack, string baseUnit = "TAB")
    {
        if (unitsPerPack <= 0)
            throw new ArgumentOutOfRangeException(nameof(unitsPerPack), "Units per pack must be greater than zero.");

        UnitsPerPack = unitsPerPack;
        BaseUnit = string.IsNullOrWhiteSpace(baseUnit) ? "TAB" : baseUnit.Trim().ToUpperInvariant();
    }

    public decimal ConvertToPacks(decimal baseQuantity) => baseQuantity / UnitsPerPack;
    public decimal ConvertToBaseUnits(decimal packQuantity) => packQuantity * UnitsPerPack;

    public override string ToString() => $"{UnitsPerPack} {BaseUnit}/Pack";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UnitsPerPack;
        yield return BaseUnit;
    }
}
