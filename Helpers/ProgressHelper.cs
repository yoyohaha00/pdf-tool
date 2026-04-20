namespace PdfTool.Helpers;

public static class ProgressHelper
{
    public static int CalculatePercentage(int current, int total)
    {
        return total == 0 ? 0 : (int)Math.Round(current * 100d / total);
    }
}
