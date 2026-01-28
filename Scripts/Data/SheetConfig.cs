using System.Collections.Generic;

public class SheetConfig
{
    public string SpreadsheetId { get; set; } = "";
    public List<SheetDefinition> Sheets { get; set; } = new();
}

public class SheetDefinition
{
    public string Name { get; set; } = "";
    public string Range { get; set; } = "";
    public string Output { get; set; } = "";
}
