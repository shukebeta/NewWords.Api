using SqlSugar;

namespace Api.Framework;

public abstract class EntityBase
{

    [SugarColumn(IsOnlyIgnoreUpdate = true)]
    public virtual long CreatedAt { get; set; }

    [SugarColumn(IsOnlyIgnoreInsert = true)]
    public virtual long? UpdatedAt { get; set; }

    [SugarColumn(IsOnlyIgnoreInsert = true)]
    public virtual long? DeletedAt { get; set; }
}