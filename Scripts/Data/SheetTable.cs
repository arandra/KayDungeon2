using System.Collections.Generic;

public class SheetTable
{
    public string Name { get; set; } = "";
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}
