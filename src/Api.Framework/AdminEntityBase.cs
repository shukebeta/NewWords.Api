using SqlSugar;

namespace Api.Framework
{
    public abstract class AdminEntityBase : EntityBase
    {
        public virtual long? UpdateBy { get; set; }
        
        [SugarColumn(IsOnlyIgnoreUpdate = false)]
        public virtual long? CreateBy { get; set; }
    }
}
