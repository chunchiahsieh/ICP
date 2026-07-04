using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ICP.Repositories;

public class ShipInfoRepository : IShipInfoRepository
{
    private readonly ApplicationDbContext _db;

    public ShipInfoRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ShipInfoHeaderListResult> SearchHeadersAsync(
        ShipInfoSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = criteria.PageSize < 1 ? 50 : Math.Min(criteria.PageSize, 200);

        var query = _db.IcpHeaders.AsNoTracking();
        query = ApplyHeaderFilters(query, criteria.Filters);

        var totalCount = await query.CountAsync(cancellationToken);
        var headers = await query
            .OrderByDescending(x => x.SaDate)
            .ThenByDescending(x => x.InvoiceNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ShipInfoHeaderListResult
        {
            TotalCount = totalCount,
            Items = headers.Select(ShipInfoEntityMapper.MapEntity).ToList()
        };
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryHeadersAsync(
        ShipInfoHeaderQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var query = _db.IcpHeaders.AsNoTracking();
        query = ApplyHeaderQueryFilters(query, criteria);

        var headers = await query
            .OrderByDescending(x => x.SaDate)
            .ThenByDescending(x => x.InvoiceNo)
            .ToListAsync(cancellationToken);

        return headers.Select(ShipInfoEntityMapper.MapEntity).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryDetailsAsync(
        ShipInfoDetailQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(criteria.HeaderKey))
        {
            return [];
        }

        var invoiceNo = ShipInfoKeyHelper.ParseInvoiceNo(criteria.HeaderKey);
        var query = _db.IcpDetails.AsNoTracking().Where(x => x.InvoiceNo == invoiceNo);
        query = ApplyDetailQueryFilters(query, criteria);

        var details = await query
            .OrderBy(x => x.InvoiceSeq)
            .ThenBy(x => x.TetPoLine)
            .ThenBy(x => x.ItemNo)
            .ToListAsync(cancellationToken);

        return details.Select(ShipInfoEntityMapper.MapEntity).ToList();
    }

    public async Task<IReadOnlyList<string>> GetDistinctHeaderValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.IcpHeaders.AsNoTracking();
        var term = search?.Trim();

        if (column.Equals("Status", StringComparison.OrdinalIgnoreCase))
        {
            var headers = await query.ToListAsync(cancellationToken);
            var statuses = headers
                .Select(ShipInfoStatusResolver.Resolve)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .AsEnumerable();

            if (!string.IsNullOrWhiteSpace(term))
            {
                statuses = statuses.Where(x => x.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return statuses.Take(200).ToList();
        }

        return column switch
        {
            "CreateDate" => await DistinctStringColumnAsync(query.Select(x => x.CreateDate), term, cancellationToken),
            "InvoiceNo" => await DistinctStringColumnAsync(query.Select(x => x.InvoiceNo), term, cancellationToken),
            "TetPo" => await DistinctStringColumnAsync(query.Select(x => x.TetPo), term, cancellationToken),
            "SaDate" => await DistinctStringColumnAsync(query.Select(x => x.SaDate), term, cancellationToken),
            "Broker" => await DistinctStringColumnAsync(query.Select(x => x.Broker), term, cancellationToken),
            "Forwarder" => await DistinctStringColumnAsync(query.Select(x => x.Forwarder), term, cancellationToken),
            "Eta" => await DistinctStringColumnAsync(query.Select(x => x.Eta), term, cancellationToken),
            "Etd" => await DistinctStringColumnAsync(query.Select(x => x.Etd), term, cancellationToken),
            "InvoiceDate" => await DistinctStringColumnAsync(query.Select(x => x.InvoiceDate), term, cancellationToken),
            "Mawb" => await DistinctStringColumnAsync(query.Select(x => x.Mawb), term, cancellationToken),
            "Hawb" => await DistinctStringColumnAsync(query.Select(x => x.Hawb), term, cancellationToken),
            "Flt" => await DistinctStringColumnAsync(query.Select(x => x.Flt), term, cancellationToken),
            "DeliveryTo" => await DistinctStringColumnAsync(query.Select(x => x.DeliveryTo), term, cancellationToken),
            "Warehouse" => await DistinctStringColumnAsync(query.Select(x => x.Warehouse), term, cancellationToken),
            "OrderType" => await DistinctStringColumnAsync(query.Select(x => x.OrderType), term, cancellationToken),
            "Deposit" => await DistinctStringColumnAsync(query.Select(x => x.Deposit), term, cancellationToken),
            "RtNo" => await DistinctStringColumnAsync(query.Select(x => x.RtNo), term, cancellationToken),
            "Notes" => await DistinctStringColumnAsync(query.Select(x => x.Notes), term, cancellationToken),
            "SapRemarks" => await DistinctStringColumnAsync(query.Select(x => x.SapRemarks), term, cancellationToken),
            _ => []
        };
    }

    public async Task<IReadOnlyList<string>> GetDistinctDetailValuesAsync(
        string column,
        string headerKey,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(headerKey))
        {
            return [];
        }

        var invoiceNo = ShipInfoKeyHelper.ParseInvoiceNo(headerKey);
        var query = _db.IcpDetails.AsNoTracking().Where(x => x.InvoiceNo == invoiceNo);
        var term = search?.Trim();

        return column switch
        {
            "InvoiceSeq" => await DistinctDoubleColumnAsync(query.Select(x => (double?)x.InvoiceSeq), term, cancellationToken),
            "TetPoLine" => await DistinctStringColumnAsync(query.Select(x => x.TetPoLine), term, cancellationToken),
            "ItemNo" => await DistinctStringColumnAsync(query.Select(x => x.ItemNo), term, cancellationToken),
            "Description" => await DistinctStringColumnAsync(query.Select(x => x.Description), term, cancellationToken),
            "Qty" => await DistinctDecimalColumnAsync(query.Select(x => x.Qty), term, cancellationToken),
            "Uom" => await DistinctStringColumnAsync(query.Select(x => x.Uom), term, cancellationToken),
            "Coo" => await DistinctStringColumnAsync(query.Select(x => x.Coo), term, cancellationToken),
            "Price" => await DistinctDoubleColumnAsync(query.Select(x => x.Price), term, cancellationToken),
            "Amount" => await DistinctDoubleColumnAsync(query.Select(x => x.Amount), term, cancellationToken),
            "Currency" => await DistinctStringColumnAsync(query.Select(x => x.Currency), term, cancellationToken),
            "CartonNo" => await DistinctDoubleColumnAsync(query.Select(x => x.CartonNo), term, cancellationToken),
            "GrossWeight" => await DistinctDecimalColumnAsync(query.Select(x => x.GrossWeight), term, cancellationToken),
            _ => []
        };
    }

    public async Task<ShipInfoDetailListResult> GetDetailsByHeaderKeyAsync(
        string headerKey,
        CancellationToken cancellationToken = default)
    {
        var details = await GetDetailEntitiesByHeaderKeyAsync(headerKey, cancellationToken);
        return new ShipInfoDetailListResult
        {
            HeaderKey = headerKey,
            Items = details.Select(ShipInfoEntityMapper.MapEntity).ToList()
        };
    }

    public async Task<IReadOnlyList<IcpDetail>> GetDetailEntitiesByHeaderKeyAsync(
        string headerKey,
        CancellationToken cancellationToken = default)
    {
        var invoiceNo = ShipInfoKeyHelper.ParseInvoiceNo(headerKey);
        return await _db.IcpDetails
            .AsNoTracking()
            .Where(x => x.InvoiceNo == invoiceNo)
            .OrderBy(x => x.InvoiceSeq)
            .ThenBy(x => x.TetPoLine)
            .ThenBy(x => x.ItemNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsHeaderByInvoiceNoAsync(string invoiceNo, CancellationToken cancellationToken = default) =>
        await _db.IcpHeaders.AsNoTracking().AnyAsync(x => x.InvoiceNo == invoiceNo, cancellationToken);

    public async Task<IcpHeader?> GetHeaderByRowKeyAsync(string headerRowKey, CancellationToken cancellationToken = default)
    {
        var (invoiceNo, tetPo) = ShipInfoKeyHelper.ParseHeaderRowKey(headerRowKey);
        return await _db.IcpHeaders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.InvoiceNo == invoiceNo && x.TetPo == tetPo, cancellationToken);
    }

    public async Task<IcpHeader?> GetHeaderForUpdateByRowKeyAsync(string headerRowKey, CancellationToken cancellationToken = default)
    {
        var (invoiceNo, tetPo) = ShipInfoKeyHelper.ParseHeaderRowKey(headerRowKey);
        return await _db.IcpHeaders.FirstOrDefaultAsync(x => x.InvoiceNo == invoiceNo && x.TetPo == tetPo, cancellationToken);
    }

    public async Task<IcpDetail?> GetDetailByKeyAsync(string detailKey, CancellationToken cancellationToken = default)
    {
        var key = ShipInfoKeyHelper.ParseDetailKey(detailKey);
        return await _db.IcpDetails.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.InvoiceNo == key.InvoiceNo
                    && x.TetPo == key.TetPo
                    && (x.TetPoLine ?? string.Empty) == key.TetPoLine
                    && (x.InvoiceSeq ?? 0) == key.InvoiceSeq
                    && (x.ItemNo ?? string.Empty) == key.ItemNo,
                cancellationToken);
    }

    public async Task<IcpDetail?> GetDetailForUpdateAsync(string detailKey, CancellationToken cancellationToken = default)
    {
        var key = ShipInfoKeyHelper.ParseDetailKey(detailKey);
        return await _db.IcpDetails.FirstOrDefaultAsync(
            x => x.InvoiceNo == key.InvoiceNo
                && x.TetPo == key.TetPo
                && (x.TetPoLine ?? string.Empty) == key.TetPoLine
                && (x.InvoiceSeq ?? 0) == key.InvoiceSeq
                && (x.ItemNo ?? string.Empty) == key.ItemNo,
            cancellationToken);
    }

    public async Task UpdateHeaderAsync(IcpHeader header, CancellationToken cancellationToken = default)
    {
        _db.IcpHeaders.Update(header);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDetailAsync(IcpDetail detail, CancellationToken cancellationToken = default)
    {
        _db.IcpDetails.Update(detail);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteHeaderWithDetailsAsync(string headerRowKey, CancellationToken cancellationToken = default)
    {
        var (invoiceNo, tetPo) = ShipInfoKeyHelper.ParseHeaderRowKey(headerRowKey);
        var details = await _db.IcpDetails
            .Where(x => x.InvoiceNo == invoiceNo)
            .ToListAsync(cancellationToken);

        if (details.Count > 0)
        {
            _db.IcpDetails.RemoveRange(details);
        }

        var header = await _db.IcpHeaders.FirstOrDefaultAsync(x => x.InvoiceNo == invoiceNo && x.TetPo == tetPo, cancellationToken);
        if (header is not null)
        {
            _db.IcpHeaders.Remove(header);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDetailAsync(string detailKey, CancellationToken cancellationToken = default)
    {
        var detail = await GetDetailForUpdateAsync(detailKey, cancellationToken);
        if (detail is null)
        {
            return;
        }

        _db.IcpDetails.Remove(detail);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAuditLogsAsync(IEnumerable<ShipInfoAuditLog> logs, CancellationToken cancellationToken = default)
    {
        var entries = logs.ToList();
        if (entries.Count == 0)
        {
            return;
        }

        _db.ShipInfoAuditLogs.AddRange(entries);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await action();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private static async Task<IReadOnlyList<string>> DistinctStringColumnAsync(
        IQueryable<string?> source,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = source.Where(x => x != null && x != string.Empty);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x!.Contains(search));
        }

        return await query
            .Select(x => x!)
            .Distinct()
            .OrderBy(x => x)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<IcpHeader> ApplyHeaderQueryFilters(
        IQueryable<IcpHeader> query,
        ShipInfoHeaderQueryModel criteria)
    {
        if (criteria.Status.Count > 0)
        {
            var normalizedStatuses = criteria.Status
                .Select(ShipInfoStatusResolver.Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            query = query.Where(x =>
                (x.Status != null && x.Status != "" && normalizedStatuses.Contains(x.Status))
                || ((x.Status == null || x.Status == "")
                    && normalizedStatuses.Contains(ShipInfoStatuses.Cancelled)
                    && x.Cancellation != null && x.Cancellation != "")
                || ((x.Status == null || x.Status == "")
                    && normalizedStatuses.Contains(ShipInfoStatuses.Processing)
                    && (x.Cancellation == null || x.Cancellation == "")));
        }

        if (criteria.CreateDate.Count > 0)
        {
            query = query.Where(x => x.CreateDate != null && criteria.CreateDate.Contains(x.CreateDate));
        }

        if (criteria.InvoiceNo.Count > 0)
        {
            query = query.Where(x => criteria.InvoiceNo.Contains(x.InvoiceNo));
        }

        if (criteria.TetPo.Count > 0)
        {
            query = query.Where(x => criteria.TetPo.Contains(x.TetPo));
        }

        if (criteria.Broker.Count > 0)
        {
            query = query.Where(x => x.Broker != null && criteria.Broker.Contains(x.Broker));
        }

        if (criteria.Eta.Count > 0)
        {
            query = query.Where(x => x.Eta != null && criteria.Eta.Contains(x.Eta));
        }

        if (criteria.SaDate.Count > 0)
        {
            query = query.Where(x => x.SaDate != null && criteria.SaDate.Contains(x.SaDate));
        }

        if (criteria.Forwarder.Count > 0)
        {
            query = query.Where(x => x.Forwarder != null && criteria.Forwarder.Contains(x.Forwarder));
        }

        if (criteria.Etd.Count > 0)
        {
            query = query.Where(x => x.Etd != null && criteria.Etd.Contains(x.Etd));
        }

        if (criteria.InvoiceDate.Count > 0)
        {
            query = query.Where(x => x.InvoiceDate != null && criteria.InvoiceDate.Contains(x.InvoiceDate));
        }

        if (criteria.Mawb.Count > 0)
        {
            query = query.Where(x => x.Mawb != null && criteria.Mawb.Contains(x.Mawb));
        }

        if (criteria.Hawb.Count > 0)
        {
            query = query.Where(x => x.Hawb != null && criteria.Hawb.Contains(x.Hawb));
        }

        if (criteria.Flt.Count > 0)
        {
            query = query.Where(x => x.Flt != null && criteria.Flt.Contains(x.Flt));
        }

        if (criteria.DeliveryTo.Count > 0)
        {
            query = query.Where(x => x.DeliveryTo != null && criteria.DeliveryTo.Contains(x.DeliveryTo));
        }

        if (criteria.Warehouse.Count > 0)
        {
            query = query.Where(x => x.Warehouse != null && criteria.Warehouse.Contains(x.Warehouse));
        }

        if (criteria.OrderType.Count > 0)
        {
            query = query.Where(x => x.OrderType != null && criteria.OrderType.Contains(x.OrderType));
        }

        if (criteria.Deposit.Count > 0)
        {
            query = query.Where(x => x.Deposit != null && criteria.Deposit.Contains(x.Deposit));
        }

        if (criteria.RtNo.Count > 0)
        {
            query = query.Where(x => x.RtNo != null && criteria.RtNo.Contains(x.RtNo));
        }

        if (criteria.Notes.Count > 0)
        {
            query = query.Where(x => x.Notes != null && criteria.Notes.Contains(x.Notes));
        }

        if (criteria.SapRemarks.Count > 0)
        {
            query = query.Where(x => x.SapRemarks != null && criteria.SapRemarks.Contains(x.SapRemarks));
        }

        return query;
    }

    private static IQueryable<IcpDetail> ApplyDetailQueryFilters(
        IQueryable<IcpDetail> query,
        ShipInfoDetailQueryModel criteria)
    {
        if (criteria.InvoiceSeq.Count > 0)
        {
            var values = ParseDoubleValues(criteria.InvoiceSeq);
            if (values.Count > 0)
            {
                query = query.Where(x => x.InvoiceSeq != null && values.Contains(x.InvoiceSeq.Value));
            }
        }

        if (criteria.TetPoLine.Count > 0)
        {
            query = query.Where(x => x.TetPoLine != null && criteria.TetPoLine.Contains(x.TetPoLine));
        }

        if (criteria.ItemNo.Count > 0)
        {
            query = query.Where(x => x.ItemNo != null && criteria.ItemNo.Contains(x.ItemNo));
        }

        if (criteria.Description.Count > 0)
        {
            query = query.Where(x => x.Description != null && criteria.Description.Contains(x.Description));
        }

        if (criteria.Qty.Count > 0)
        {
            var values = ParseDecimalValues(criteria.Qty);
            if (values.Count > 0)
            {
                query = query.Where(x => x.Qty != null && values.Contains(x.Qty.Value));
            }
        }

        if (criteria.Uom.Count > 0)
        {
            query = query.Where(x => x.Uom != null && criteria.Uom.Contains(x.Uom));
        }

        if (criteria.Coo.Count > 0)
        {
            query = query.Where(x => x.Coo != null && criteria.Coo.Contains(x.Coo));
        }

        if (criteria.Price.Count > 0)
        {
            var values = ParseDoubleValues(criteria.Price);
            if (values.Count > 0)
            {
                query = query.Where(x => x.Price != null && values.Contains(x.Price.Value));
            }
        }

        if (criteria.Amount.Count > 0)
        {
            var values = ParseDoubleValues(criteria.Amount);
            if (values.Count > 0)
            {
                query = query.Where(x => x.Amount != null && values.Contains(x.Amount.Value));
            }
        }

        if (criteria.Currency.Count > 0)
        {
            query = query.Where(x => x.Currency != null && criteria.Currency.Contains(x.Currency));
        }

        if (criteria.CartonNo.Count > 0)
        {
            var values = ParseDoubleValues(criteria.CartonNo);
            if (values.Count > 0)
            {
                query = query.Where(x => x.CartonNo != null && values.Contains(x.CartonNo.Value));
            }
        }

        if (criteria.GrossWeight.Count > 0)
        {
            var values = ParseDecimalValues(criteria.GrossWeight);
            if (values.Count > 0)
            {
                query = query.Where(x => x.GrossWeight != null && values.Contains(x.GrossWeight.Value));
            }
        }

        return query;
    }

    private static List<double> ParseDoubleValues(IEnumerable<string> values)
    {
        var result = new List<double>();
        foreach (var value in values)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    private static List<decimal> ParseDecimalValues(IEnumerable<string> values)
    {
        var result = new List<decimal>();
        foreach (var value in values)
        {
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                result.Add(parsed);
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<string>> DistinctDoubleColumnAsync(
        IQueryable<double?> source,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = source.Where(x => x != null);
        var values = await query
            .Select(x => x!.Value)
            .Distinct()
            .OrderBy(x => x)
            .Take(200)
            .ToListAsync(cancellationToken);

        var strings = values
            .Select(x => x.ToString(CultureInfo.InvariantCulture))
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            strings = strings.Where(x => x.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return strings.ToList();
    }

    private static async Task<IReadOnlyList<string>> DistinctDecimalColumnAsync(
        IQueryable<decimal?> source,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = source.Where(x => x != null);
        var values = await query
            .Select(x => x!.Value)
            .Distinct()
            .OrderBy(x => x)
            .Take(200)
            .ToListAsync(cancellationToken);

        var strings = values
            .Select(x => x.ToString(CultureInfo.InvariantCulture))
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            strings = strings.Where(x => x.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return strings.ToList();
    }

    private static IQueryable<IcpHeader> ApplyHeaderFilters(
        IQueryable<IcpHeader> query,
        IReadOnlyDictionary<string, string?> filters)
    {
        if (filters.Count == 0)
        {
            return query;
        }

        if (filters.TryGetValue("Status", out var status) && !string.IsNullOrWhiteSpace(status))
        {
            query = ApplyHeaderQueryFilters(query, new ShipInfoHeaderQueryModel { Status = [status] });
        }

        if (filters.TryGetValue("CreateDate", out var createDate) && !string.IsNullOrWhiteSpace(createDate))
        {
            query = query.Where(x => x.CreateDate != null && x.CreateDate.Contains(createDate));
        }

        if (filters.TryGetValue("InvoiceNo", out var invoiceNo) && !string.IsNullOrWhiteSpace(invoiceNo))
        {
            query = query.Where(x => x.InvoiceNo.Contains(invoiceNo));
        }

        if (filters.TryGetValue("TetPo", out var tetPo) && !string.IsNullOrWhiteSpace(tetPo))
        {
            query = query.Where(x => x.TetPo.Contains(tetPo));
        }

        if (filters.TryGetValue("Broker", out var broker) && !string.IsNullOrWhiteSpace(broker))
        {
            query = query.Where(x => x.Broker == broker);
        }

        if (filters.TryGetValue("Eta", out var eta) && !string.IsNullOrWhiteSpace(eta))
        {
            query = query.Where(x => x.Eta != null && x.Eta.Contains(eta));
        }

        if (filters.TryGetValue("SaDate", out var saDate) && !string.IsNullOrWhiteSpace(saDate))
        {
            query = query.Where(x => x.SaDate != null && x.SaDate.Contains(saDate));
        }

        return query;
    }
}
