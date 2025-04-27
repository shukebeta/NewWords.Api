using System.Linq.Expressions;
using Api.Framework.Models;
using SqlSugar;

namespace Api.Framework;

public class RepositoryBase<TEntity>(ISqlSugarClient dbClient) : IRepositoryBase<TEntity>
    where TEntity : class, new()
{
    public ISqlSugarClient db => dbClient;

    public virtual async Task<bool> UpsertAsync(TEntity entity, Expression<Func<TEntity, bool>> where)
    {
        var existingEntity = await GetFirstOrDefaultAsync(where);
        if (existingEntity == null)
        {
            return await db.Insertable(entity).ExecuteCommandIdentityIntoEntityAsync();
        }
        return await db.Updateable(entity).ExecuteCommandHasChangeAsync();
    }

    #region Insert && Update

    public virtual async Task<bool> InsertAsync(TEntity entity)
    {
        return await db.Insertable(entity).ExecuteCommandIdentityIntoEntityAsync();
    }

    public virtual async Task<int> InsertBulkAsync(IList<TEntity> list)
    {
        var newList = new List<TEntity>();
        newList.AddRange(list);
        return await db.Insertable(newList).ExecuteCommandAsync();
    }

    public virtual async Task<long> InsertReturnIdentityAsync(TEntity entity)
    {
        return await db.Insertable(entity).ExecuteReturnBigIdentityAsync();
    }

    public virtual async Task<bool> UpdateAsync(TEntity entity)
    {
        return await db.Updateable(entity).ExecuteCommandHasChangeAsync();
    }

    public virtual async Task<int> UpdateAsync(IList<TEntity> entities)
    {
        var newList = new List<TEntity>();
        newList.AddRange(entities);
        return await db.Updateable(newList).ExecuteCommandAsync();
    }

    public virtual async Task<bool> UpdateAsync(TEntity entity, Expression<Func<TEntity, bool>> where)
    {
        if (null == where)
        {
            throw new Exception(
                "If you don't have any condition, please use the method UpdateAsync without the argument of where.");
        }

        return await db.Updateable(entity).Where(where).ExecuteCommandHasChangeAsync();
    }

    public virtual async Task<int> UpdateAsync(IList<TEntity> entities, Expression<Func<TEntity, bool>> where)
    {
        if (null == where)
        {
            throw new Exception(
                "If you don't have any condition, please use the method UpdateAsync without the argument of where.");
        }

        var newList = new List<TEntity>();
        newList.AddRange(entities);
        return await db.Updateable(newList).Where(where).ExecuteCommandAsync();
    }

    public virtual async Task<int> UpdateAsync(Expression<Func<TEntity, TEntity>> columns,
        Expression<Func<TEntity, bool>> where)
    {
        return await db.Updateable<TEntity>().SetColumns(columns).Where(where).ExecuteCommandAsync();
    }

    public virtual async Task<int> UpdateCountAsync(Expression<Func<TEntity, bool>> set,
        Expression<Func<TEntity, bool>> where)
    {
        return await db.Updateable<TEntity>().SetColumns(set).Where(where).ExecuteCommandAsync();
    }

    public virtual async Task<int> UpdateMultipleFieldAsync(IList<Expression<Func<TEntity, bool>>> columnList,
        Expression<Func<TEntity, bool>> where)
    {
        var query = db.Updateable<TEntity>();
        foreach (var col in columnList)
        {
            query = query.SetColumns(col);
        }

        return await query.Where(where).ExecuteCommandAsync();
    }

    #endregion

    #region Delete

    public virtual async Task<bool> DeleteAsync(object id)
    {
        return await db.Deleteable<TEntity>(id).ExecuteCommandHasChangeAsync();
    }

    public virtual async Task<bool> DeleteAsync(TEntity entity)
    {
        return await db.Deleteable(entity).ExecuteCommandHasChangeAsync();
    }

    public virtual async Task<int> DeleteReturnRowsAsync(Expression<Func<TEntity, bool>> where)
    {
        return await db.Deleteable<TEntity>().Where(where).ExecuteCommandAsync();
    }

    public virtual async Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> where)
    {
        return await db.Deleteable<TEntity>().Where(where).ExecuteCommandHasChangeAsync();
    }

    public virtual async Task<bool> DeleteBulkAsync(object[] ids)
    {
        return await db.Deleteable<TEntity>().In(ids).ExecuteCommandHasChangeAsync();
    }

    #endregion

    #region Query

    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? where = null)
    {
        return await db.Queryable<TEntity>().WhereIF(where != null, where).CountAsync();
    }

    /// <summary>
    /// check if one record exists
    /// </summary>
    /// <param name="where"></param>
    /// <returns></returns>
    public virtual async Task<bool> IsExistAsync(Expression<Func<TEntity, bool>> where)
    {
        return await db.Queryable<TEntity>().AnyAsync(where);
    }

    public virtual async Task<IList<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? where)
    {
        return await db.Queryable<TEntity>().WhereIF(where != null, where).ToListAsync();
    }

    public virtual ISugarQueryable<TEntity> GetQueryableList(Expression<Func<TEntity, bool>>? where)
    {
        return db.Queryable<TEntity>().WhereIF(where != null, where);
    }

    public virtual async Task<IList<TEntity>> GetListAsync(Expression<Func<TEntity, bool>>? where,
        Expression<Func<TEntity, object>>? orderBy = null, bool isAsc = true)
    {
        return await db.Queryable<TEntity>()
            .OrderByIF(orderBy != null, orderBy, isAsc ? OrderByType.Asc : OrderByType.Desc)
            .WhereIF(where != null, where).ToListAsync();
    }

    public virtual async Task<IList<TEntity>> GetListAsync(int top = 10,
        Expression<Func<TEntity, bool>>? where = null,
        Expression<Func<TEntity, object>>? orderBy = null, bool isAsc = true)
    {
        return await db.Queryable<TEntity>()
            .OrderByIF(orderBy != null, orderBy, isAsc ? OrderByType.Asc : OrderByType.Desc)
            .WhereIF(where != null, where).Take(top).ToListAsync();
    }

    public virtual async Task<IList<TEntity>> GetListByIdsAsync(string[] ids)
    {
        return await db.Queryable<TEntity>().In(ids).ToListAsync();
    }

    public virtual async Task<IList<TEntity>> GetListByIdsAsync(long[] ids)
    {
        return await db.Queryable<TEntity>().In(ids).ToListAsync();
    }

    public virtual async Task<IList<TEntity>> GetListByIdsAsync(int[] ids)
    {
        return await db.Queryable<TEntity>().In(ids).ToListAsync();
    }

    public virtual async Task<TEntity?> GetFirstOrDefaultAsync(Expression<Func<TEntity, bool>>? @where, string? orderBy = null)
    {
        var queryAble = db.Queryable<TEntity>().WhereIF(null != where, where);
        if (null != orderBy)
        {
            queryAble.OrderBy(orderBy);
        }

        var result = await queryAble.Take(1).ToListAsync();

        return result.FirstOrDefault();
    }

    public virtual async Task<IList<TEntity>> GetListByIdsAsync<T>(T[] ids)
    {
        return await db.Queryable<TEntity>().In(ids).ToListAsync();
    }

    public virtual async Task<IList<TEntity>> GetTopListAsync(int top, Expression<Func<TEntity, bool>>? where = null,
        List<string>? orderBy = null)
    {
        var queryAble = db.Queryable<TEntity>().WhereIF(null != where, where);
        if (null != orderBy)
        {
            foreach (string o in orderBy)
            {
                queryAble.OrderBy(o);
            }
        }

        var result = await queryAble.Take(top).ToListAsync();

        return result;
    }

    public virtual async Task<PageData<TEntity>> GetPageListAsync(int pageIndex = 1, int pageSize = 20,
        Expression<Func<TEntity, bool>>? where = null, List<string>? orderBy = null)
    {
        var pageData = new PageData<TEntity>
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };
        RefAsync<int> totalCount = 0;
        var queryAble = db.Queryable<TEntity>().WhereIF(null != where, where);
        if (null != orderBy)
        {
            foreach (string o in orderBy)
            {
                queryAble.OrderBy(o);
            }
        }

        var result = await queryAble.ToPageListAsync(pageIndex, pageSize, totalCount);
        pageData.TotalCount = totalCount;
        pageData.DataList = result;
        return pageData;
    }

    public virtual async Task<PageData<TEntity>> GetPageListAsync(int pageIndex = 1, int pageSize = 20,
        SortedDictionary<string, object>? where = null, List<string>? orderBy = null)
    {
        var pageData = new PageData<TEntity>
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };
        RefAsync<int> totalCount = 0;
        var queryAble = db.Queryable<TEntity>();
        if (null != where)
        {
            foreach (string w in where.Keys)
            {
                queryAble.WhereIF(true, w, where.GetValueOrDefault(w));
            }
        }

        if (null != orderBy)
        {
            foreach (string o in orderBy)
            {
                queryAble.OrderBy(o);
            }
        }

        var result = await queryAble.ToPageListAsync(pageIndex, pageSize, totalCount);
        pageData.TotalCount = totalCount;
        pageData.DataList = result;
        return pageData;
    }

    public virtual async Task<PageData<TEntity>> GetPageListAsync(int pageIndex = 1, int pageSize = 20,
        Expression<Func<TEntity, bool>>? where = null, Expression<Func<TEntity, object>>? orderBy = null,
        bool isAsc = true)
    {
        var pageData = new PageData<TEntity>
        {
            PageIndex = pageIndex,
            PageSize = pageSize
        };
        RefAsync<int> totalCount = 0;
        var result = await db.Queryable<TEntity>().WhereIF(null != where, where)
            .OrderByIF(orderBy != null, orderBy, isAsc ? OrderByType.Asc : OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, totalCount);
        pageData.TotalCount = totalCount;
        pageData.DataList = result;
        return pageData;
    }

    public virtual async Task<TEntity> GetSingleAsync(object objId)
    {
        return await db.Queryable<TEntity>().InSingleAsync(objId);
    }

    public virtual async Task<TEntity> GetSingleAsync(Expression<Func<TEntity, bool>>? where = null)
    {
        return await db.Queryable<TEntity>().WhereIF(null != where, where).SingleAsync();
    }

    #endregion

    #region Ado

    public virtual async Task<T?> FetchOneAsync<T>(string sql, object? param = null)
    {
        var r = await db.Ado.GetScalarAsync(sql, param);
        if (Convert.IsDBNull(r))
        {
            return default;
        }

        return (T) r;
    }

    public virtual async Task<int> ExecuteSqlAsync(string sql, object? param = null)
    {
        return await db.Ado.ExecuteCommandAsync(sql, param);
    }

    public virtual async Task<IList<TEntity>> QueryAsync(String sql, object? param = null)
    {
        return await db.Ado.SqlQueryAsync<TEntity>(sql, param);
    }

    public virtual async Task<TEntity> QuerySingleAsync(String sql, object? param = null)
    {
        return await db.Ado.SqlQuerySingleAsync<TEntity>(sql, param);
    }

    #endregion

    public static T? GetPropertyValue<T>(object obj, string propName)
    {
        // return (T) obj.GetType().GetProperty(propName)?.GetValue(obj, null);
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        if (string.IsNullOrEmpty(propName))
        {
            throw new ArgumentException("Property name cannot be null or empty.", nameof(propName));
        }

        var propertyInfo = obj.GetType().GetProperty(propName);

        if (propertyInfo == null)
        {
            throw new ArgumentException($"Property '{propName}' not found on type '{obj.GetType().FullName}'.");
        }

        try
        {
            var value = propertyInfo.GetValue(obj);

            if (value == null)
            {
                return default;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            // If the value is not of type T, attempt to convert it
            return (T) Convert.ChangeType(value, typeof(T));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Error getting property value for '{propName}' on type '{obj.GetType().FullName}'. See inner exception for details.",
                ex);
        }
    }
}
