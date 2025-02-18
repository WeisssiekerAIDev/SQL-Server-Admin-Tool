using System.Data;
using System.IO;
using System.Globalization;
using CsvHelper;
using OfficeOpenXml;
using Serilog;

namespace SQLServerAdmin.Services
{
    /// <summary>
    /// Service für den Export von Query-Ergebnissen
    /// </summary>
    public class QueryExportService
    {
        /// <summary>
        /// Exportiert die Daten als CSV-Datei
        /// </summary>
        public async Task ExportToCsvAsync(DataTable data, string filePath)
        {
            try
            {
                using var writer = new StreamWriter(filePath);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

                // Spaltennamen schreiben
                foreach (DataColumn column in data.Columns)
                {
                    csv.WriteField(column.ColumnName);
                }
                await csv.NextRecordAsync();

                // Daten schreiben
                foreach (DataRow row in data.Rows)
                {
                    for (var i = 0; i < data.Columns.Count; i++)
                    {
                        csv.WriteField(row[i]);
                    }
                    await csv.NextRecordAsync();
                }

                Log.Information("Daten erfolgreich als CSV exportiert: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim CSV-Export");
                throw;
            }
        }

        /// <summary>
        /// Exportiert die Daten als Excel-Datei
        /// </summary>
        public async Task ExportToExcelAsync(DataTable data, string filePath)
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Daten");

                // Spaltennamen in die erste Zeile
                for (int col = 0; col < data.Columns.Count; col++)
                {
                    worksheet.Cells[1, col + 1].Value = data.Columns[col].ColumnName;
                    worksheet.Cells[1, col + 1].Style.Font.Bold = true;
                }

                // Daten ab Zeile 2
                for (int row = 0; row < data.Rows.Count; row++)
                {
                    for (int col = 0; col < data.Columns.Count; col++)
                    {
                        worksheet.Cells[row + 2, col + 1].Value = data.Rows[row][col];
                    }
                }

                worksheet.Cells.AutoFitColumns();

                await package.SaveAsAsync(new FileInfo(filePath));
                Log.Information("Daten erfolgreich als Excel exportiert: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Fehler beim Excel-Export");
                throw;
            }
        }
    }
}
