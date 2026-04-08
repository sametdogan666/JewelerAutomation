namespace JewelerAutomation.Application.Utilities;

/// <summary>
/// Sepet kaydında istemci genelde yalnızca gün seçer (UTC 00:00). Bu durumda kayıt anının saat/dakika/saniyesini ekler;
/// istemci açık saat gönderdiye değişmez.
/// </summary>
public static class TransactionDatePrecision
{
    public static DateTime ApplySavePrecisionUtc(DateTime requested)
    {
        var now = DateTime.UtcNow;
        var u = requested.Kind switch
        {
            DateTimeKind.Utc => requested,
            DateTimeKind.Local => requested.ToUniversalTime(),
            _ => DateTime.SpecifyKind(requested, DateTimeKind.Utc),
        };

        if (u.TimeOfDay != TimeSpan.Zero)
            return u;

        return new DateTime(u.Year, u.Month, u.Day, 0, 0, 0, DateTimeKind.Utc).Add(now.TimeOfDay);
    }
}
