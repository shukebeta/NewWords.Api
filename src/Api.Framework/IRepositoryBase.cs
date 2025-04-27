using System.Linq.Expressions;
using Api.Framework.Models;
using SqlSugar;

namespace Api.Framework;

public interface IRepositoryBase<TEntity> where TEntity : class, new()
{
    ISqlSugarClient db { get; }
    Task<bool> UpsertAsync(TEntity entity, Expression<Func<TEntity, bool>> where);
    #region Insert methods

    Task<bool> InsertAsync(TEntity entity);
    Task<long> InsertReturnIdentityAsync(TEntity entity);
    Task<int> InsertBulkAsync(IList<TEntity> list);

    #endregion

    #region Update Methods

    Task<bool> UpdateAsync(TEntity entity);
    Task<bool> UpdateAsync(TEntity entity, Expression<Func<TEntity, bool>> where);
    Task<int> UpdateAsync(IList<TEntity> entities);
    Task<int> UpdateAsync(IList<TEntity> entities, Expression<Func<TEntity, bool>> where);
    Task<int> UpdateAsync(Expression<Func<TEntity, TEntity>> columns, Expression<Func<TEntity, bool>> where);
    Task<int> UpdateCountAsync(Expression<Func<TEntity, bool>> columns, Expression<Func<TEntity, bool>> where);
    Task<int> UpdateMultipleFieldAsync(IList<Expression<Func<TEntity, bool>>> columnList, Expression<Func<TEntity, bool>> where);
    #endregion


    #region Delete Methods

    Task<bool> DeleteAsync(object id);

    Task<bool> DeleteAsync(TEntity entity);

    Task<bool> DeleteBulkAsync(object[] ids);

    Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> where);
    Task<int> DeleteReturnRowsAsync(Expression<Func<TEntity, bool>> where);

    #endregion

    #region Exist method

    Task<bool> IsExistAsync(Expression<Func<TEntity, bool>> where);

    #endregion

    #region Select Methods

    Task<TEntity> GetSingleAsync(object objId);

    Task<TEntity> GetSingleAsync(Expression<Func<TEntity, bool>>? where);


    /// <summary>
    /// OrderBy can be 'fieldName' or 'fieldName ASC|DESC'
    /// </summary>
    /// <param name="where"></param>
    /// <param name="orderBy"></param>
    /// <returns></returns>
    Task<TEntity?> GetFirstOrDefaultAsync(Expression<Func<TEntity, bool>>? where, string? orderBy = null);

    #endregion

    #region Get List

    Task<IList<TEntity>> GetListByIdsAsync<T>(T[] ids);

    Task<IList<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? where = null);

    Task<IList<TEntity>> GetListAsync(Expression<Func<TEntity, bool>>? where,
        Expression<Func<TEntity, object>>? orderBy = null, bool isAsc = true);

    Task<IList<TEntity>> GetListAsync(int top = 10, Expression<Func<TEntity, bool>>? where = null,
        Expression<Func<TEntity, object>>? orderBy = null, bool isAsc = true);

    Task<PageData<TEntity>> GetPageListAsync(int pageIndex = 1, int pageSize = 20,
        Expression<Func<TEntity, bool>>? where = null, Expression<Func<TEntity, object>>? orderBy = null,
        bool isAsc = true);

    Task<IList<TEntity>> GetTopListAsync(int top, Expression<Func<TEntity, bool>>? where = null, List<string>? orderBy = null);

    Task<PageData<TEntity>> GetPageListAsync(int pageIndex = 1, int pageSize = 20,
        Expression<Func<TEntity, bool>>? where = null, List<string>? orderBy = null);

    Task<PageData<TEntity>> GetPageListAsync(int pageIndex = 1, int pageSize = 20,
        SortedDictionary<string, object>? where = null, List<string>? orderBy = null);

    #endregion

    #region Others

    Task<int> CountAsync(Expression<Func<TEntity, bool>>? where = null);

    #endregion

    #region ADO

    Task<IList<TEntity>> QueryAsync(string sql, object? param = null);
    Task<TEntity> QuerySingleAsync(string sql, object? param = null);
    /// <summary>
    /// Get first column from first row
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="param"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task<T?> FetchOneAsync<T>(string sql, object? param = null);

    Task<int> ExecuteSqlAsync(string sql, object? param = null);

    #endregion
}
