namespace DepinTracker.Infrastructure.Persistence;

using System.Data;
using System.Globalization;
using Dapper;

/// <summary>
/// Dapper type handlers that map types SQLite has no native column type for to TEXT
/// in a deterministic, culture-invariant, round-trippable form. Registering these
/// keeps every value exact (especially <see cref="decimal"/> money amounts) and
/// avoids relying on ambient culture. <see cref="Register"/> is idempotent.
/// </summary>
public static class SqliteTypeHandlers
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new GuidHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
        SqlMapper.AddTypeHandler(new DecimalHandler());
        _registered = true;
    }

    private sealed class GuidHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("D");
        }

        public override Guid Parse(object value) => Guid.Parse((string)value);
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("O", CultureInfo.InvariantCulture);
        }

        public override DateTimeOffset Parse(object value) =>
            DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        public override DateOnly Parse(object value) =>
            DateOnly.ParseExact((string)value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private sealed class DecimalHandler : SqlMapper.TypeHandler<decimal>
    {
        public override void SetValue(IDbDataParameter parameter, decimal value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString(CultureInfo.InvariantCulture);
        }

        public override decimal Parse(object value) =>
            value is decimal d ? d : decimal.Parse((string)value, CultureInfo.InvariantCulture);
    }
}
