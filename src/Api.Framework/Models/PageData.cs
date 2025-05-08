namespace Api.Framework.Models;

public class PageData<TEntity> where TEntity : class,new()
{

    public int PageIndex { get; set; } = 1;
    public int PageSize { init; get; }
    public int TotalCount { get; set; }
    public IList<TEntity>? DataList { get; set; }

}
