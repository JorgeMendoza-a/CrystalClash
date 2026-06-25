public class Cell
{
    public int x;
    public int y;
    public Mineral mineral;

    public Cell(int x, int y)
    {
        this.x = x;
        this.y = y;
        mineral = null;
    }

    public bool IsEmpty()
    {
        return mineral == null;
    }
}