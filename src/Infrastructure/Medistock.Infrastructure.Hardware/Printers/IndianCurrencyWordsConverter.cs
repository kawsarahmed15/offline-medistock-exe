using System;

namespace Medistock.Infrastructure.Hardware.Printers;

public static class IndianCurrencyWordsConverter
{
    private static readonly string[] Units =
    {
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    };

    private static readonly string[] Tens =
    {
        "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
    };

    public static string ConvertToWords(decimal amount)
    {
        if (amount == 0) return "Zero Rupees Only";

        long integral = (long)Math.Truncate(Math.Abs(amount));
        int paise = (int)Math.Round((Math.Abs(amount) - integral) * 100);

        string result = "";
        if (integral > 0)
        {
            result = ConvertIntegralPart(integral) + " Rupees";
        }

        if (paise > 0)
        {
            string paiseStr = ConvertHundreds(paise) + " Paise";
            result = string.IsNullOrEmpty(result) ? paiseStr + " Only" : result + " and " + paiseStr + " Only";
        }
        else
        {
            result += " Only";
        }

        return result.Trim();
    }

    private static string ConvertIntegralPart(long n)
    {
        if (n == 0) return "";

        string words = "";

        if ((n / 10000000) > 0)
        {
            words += ConvertIntegralPart(n / 10000000) + " Crore ";
            n %= 10000000;
        }

        if ((n / 100000) > 0)
        {
            words += ConvertHundreds((int)(n / 100000)) + " Lakh ";
            n %= 100000;
        }

        if ((n / 1000) > 0)
        {
            words += ConvertHundreds((int)(n / 1000)) + " Thousand ";
            n %= 1000;
        }

        if ((n / 100) > 0)
        {
            words += ConvertHundreds((int)(n / 100)) + " Hundred ";
            n %= 100;
        }

        if (n > 0)
        {
            words += ConvertHundreds((int)n) + " ";
        }

        return words.Trim();
    }

    private static string ConvertHundreds(int n)
    {
        if (n == 0) return "";
        if (n < 20) return Units[n];
        if (n < 100)
        {
            int t = n / 10;
            int u = n % 10;
            return u > 0 ? $"{Tens[t]}-{Units[u]}" : Tens[t];
        }
        return $"{Units[n / 100]} Hundred" + (n % 100 > 0 ? " " + ConvertHundreds(n % 100) : "");
    }
}
