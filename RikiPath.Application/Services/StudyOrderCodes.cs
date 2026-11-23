using System.Security.Cryptography;

namespace RikiPath.Application.Services;

/// <summary>
/// PayOS order codes for self-study purchases.
/// <para>
/// The legacy code was <c>900_000_000 + purchaseId</c>. Several databases (local, shared, other machines) share one PayOS
/// channel and reuse the same id range, so PayOS rejected new orders whose code another environment had already used
/// ("Đơn thanh toán đã tồn tại", code 231) and checkout failed with HTTP 502.
/// </para>
/// <para>
/// V2 codes are <c>1_000_000_000_000 + purchaseId × 100_000 + nonce</c> with a random 5-digit nonce, drawn again on a
/// duplicate. The purchase id stays decodable for webhook routing, while the binding to the order is the stored
/// PayOS <c>paymentLinkId</c> (checked on every webhook and reconciliation). Max value stays under 2^53−1 (PayOS limit).
/// </para>
/// </summary>
public static class StudyOrderCodes
{
    public const long LegacyPrefix = 900_000_000;
    public const long V2Base = 1_000_000_000_000;
    private const long NonceSpace = 100_000;
    private const long MaxPurchaseId = 80_000_000_000;

    public static long Create(int purchaseId)
    {
        if (purchaseId <= 0) throw new ArgumentOutOfRangeException(nameof(purchaseId));
        return V2Base + purchaseId * NonceSpace + RandomNumberGenerator.GetInt32(1, (int)NonceSpace);
    }

    public static bool IsStudyOrderCode(long orderCode)
        => orderCode >= V2Base || orderCode is >= LegacyPrefix and <= int.MaxValue;

    /// <summary>
    /// What a Pending order stores in <c>PaymentTransactionId</c>: <c>{paymentLinkId}|{orderCode}</c>, binding the order to the
    /// exact PayOS link and code (no schema change; on activation the field holds the bank reference as before).
    /// </summary>
    public static string PendingReference(string paymentLinkId, long orderCode)
        => $"{paymentLinkId}|{orderCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Reads the link id and order code bound to a Pending order; legacy rows hold only the link id and the 900M code.</summary>
    public static bool TryReadPendingReference(string? stored, int purchaseId, out string paymentLinkId, out long orderCode)
    {
        paymentLinkId = string.Empty;
        orderCode = 0;
        if (string.IsNullOrWhiteSpace(stored)) return false;
        var separator = stored.IndexOf('|');
        if (separator < 0)
        {
            paymentLinkId = stored;
            orderCode = LegacyPrefix + purchaseId;
            return true;
        }
        paymentLinkId = stored[..separator];
        return paymentLinkId.Length > 0
            && long.TryParse(stored[(separator + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out orderCode)
            && TryGetPurchaseId(orderCode, out var decoded) && decoded == purchaseId;
    }

    public static bool TryGetPurchaseId(long orderCode, out int purchaseId)
    {
        purchaseId = 0;
        long id;
        if (orderCode >= V2Base)
        {
            id = (orderCode - V2Base) / NonceSpace;
            if (id <= 0 || id > MaxPurchaseId || (orderCode - V2Base) % NonceSpace == 0) return false;
        }
        else if (orderCode is >= LegacyPrefix and <= int.MaxValue)
        {
            id = orderCode - LegacyPrefix;
        }
        else return false;

        if (id <= 0 || id > int.MaxValue) return false;
        purchaseId = (int)id;
        return true;
    }
}
