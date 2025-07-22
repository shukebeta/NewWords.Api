using NewWords.Api.Entities;
using NSubstitute;
using SqlSugar;

namespace NewWords.Api.Tests.Helpers
{
    /// <summary>
    /// Helper class for creating mock database operations with SqlSugar.
    /// </summary>
    public static class MockDatabaseHelper
    {
        public static ISqlSugarClient CreateMockDatabase()
        {
            return Substitute.For<ISqlSugarClient>();
        }

        public static void SetupQueryable<T>(ISqlSugarClient mockDb, List<T> data) where T : class, new()
        {
            var queryable = Substitute.For<ISugarQueryable<T>>();
            queryable.ToListAsync().Returns(Task.FromResult(data));
            queryable.FirstAsync().Returns(data.FirstOrDefault());
            queryable.CountAsync().Returns(data.Count);
            
            // Setup method chaining for Where, OrderBy, etc.
            queryable.Where(Arg.Any<System.Linq.Expressions.Expression<Func<T, bool>>>())
                .Returns(queryable);
            queryable.OrderBy(Arg.Any<System.Linq.Expressions.Expression<Func<T, object>>>(), Arg.Any<OrderByType>())
                .Returns(queryable);
            queryable.ToPageListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<RefAsync<int>>())
                .Returns(callInfo =>
                {
                    var pageNumber = callInfo.ArgAt<int>(0);
                    var pageSize = callInfo.ArgAt<int>(1);
                    var totalCount = callInfo.ArgAt<RefAsync<int>>(2);
                    
                    totalCount.Value = data.Count;
                    var skip = (pageNumber - 1) * pageSize;
                    var pagedData = data.Skip(skip).Take(pageSize).ToList();
                    
                    return Task.FromResult(pagedData);
                });

            mockDb.Queryable<T>().Returns(queryable);
        }

        public static void SetupUserSubscriptionQuery(
            ISqlSugarClient mockDb, 
            List<UserSubscription> subscriptions)
        {
            var queryable = Substitute.For<ISugarQueryable<UserSubscription>>();
            
            // Setup basic query operations
            queryable.ToListAsync().Returns(Task.FromResult(subscriptions));
            queryable.FirstAsync().Returns(subscriptions.FirstOrDefault());
            queryable.CountAsync().Returns(subscriptions.Count);

            // Setup method chaining
            queryable.Where(Arg.Any<System.Linq.Expressions.Expression<Func<UserSubscription, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.ArgAt<System.Linq.Expressions.Expression<Func<UserSubscription, bool>>>(0);
                    var compiledPredicate = predicate.Compile();
                    var filteredData = subscriptions.Where(compiledPredicate).ToList();
                    
                    var filteredQueryable = Substitute.For<ISugarQueryable<UserSubscription>>();
                    filteredQueryable.FirstAsync().Returns(filteredData.FirstOrDefault());
                    filteredQueryable.ToListAsync().Returns(Task.FromResult(filteredData));
                    filteredQueryable.CountAsync().Returns(filteredData.Count);
                    
                    // Allow further chaining
                    filteredQueryable.OrderBy(Arg.Any<System.Linq.Expressions.Expression<Func<UserSubscription, object>>>(), Arg.Any<OrderByType>())
                        .Returns(filteredQueryable);
                    
                    return filteredQueryable;
                });

            queryable.OrderBy(Arg.Any<System.Linq.Expressions.Expression<Func<UserSubscription, object>>>(), Arg.Any<OrderByType>())
                .Returns(queryable);

            mockDb.Queryable<UserSubscription>().Returns(queryable);
        }

        public static void SetupUserWordQuery(
            ISqlSugarClient mockDb, 
            List<UserWord> userWords)
        {
            var queryable = Substitute.For<ISugarQueryable<UserWord>>();
            
            queryable.Where(Arg.Any<System.Linq.Expressions.Expression<Func<UserWord, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.ArgAt<System.Linq.Expressions.Expression<Func<UserWord, bool>>>(0);
                    var compiledPredicate = predicate.Compile();
                    var filteredData = userWords.Where(compiledPredicate).ToList();
                    
                    var filteredQueryable = Substitute.For<ISugarQueryable<UserWord>>();
                    filteredQueryable.CountAsync().Returns(filteredData.Count);
                    filteredQueryable.ToListAsync().Returns(Task.FromResult(filteredData));
                    
                    return filteredQueryable;
                });

            queryable.CountAsync().Returns(userWords.Count);
            mockDb.Queryable<UserWord>().Returns(queryable);
        }

        public static void SetupSubscriptionHistoryQuery(
            ISqlSugarClient mockDb, 
            List<SubscriptionHistory> history)
        {
            var queryable = Substitute.For<ISugarQueryable<SubscriptionHistory>>();
            
            queryable.Where(Arg.Any<System.Linq.Expressions.Expression<Func<SubscriptionHistory, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.ArgAt<System.Linq.Expressions.Expression<Func<SubscriptionHistory, bool>>>(0);
                    var compiledPredicate = predicate.Compile();
                    var filteredData = history.Where(compiledPredicate).ToList();
                    
                    var filteredQueryable = Substitute.For<ISugarQueryable<SubscriptionHistory>>();
                    filteredQueryable.OrderBy(Arg.Any<System.Linq.Expressions.Expression<Func<SubscriptionHistory, object>>>(), Arg.Any<OrderByType>())
                        .Returns(filteredQueryable);
                    filteredQueryable.ToPageListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<RefAsync<int>>())
                        .Returns(callInfo =>
                        {
                            var pageNumber = callInfo.ArgAt<int>(0);
                            var pageSize = callInfo.ArgAt<int>(1);
                            var totalCount = callInfo.ArgAt<RefAsync<int>>(2);
                            
                            totalCount.Value = filteredData.Count;
                            var skip = (pageNumber - 1) * pageSize;
                            var pagedData = filteredData.Skip(skip).Take(pageSize).ToList();
                            
                            return Task.FromResult(pagedData);
                        });
                    
                    return filteredQueryable;
                });

            mockDb.Queryable<SubscriptionHistory>().Returns(queryable);
        }

        public static void SetupInsertable<T>(ISqlSugarClient mockDb, int returnId = 1) where T : class, new()
        {
            var insertable = Substitute.For<IInsertable<T>>();
            insertable.ExecuteReturnIdentityAsync().Returns(Task.FromResult(returnId));
            insertable.ExecuteCommandAsync().Returns(Task.FromResult(1));
            
            mockDb.Insertable(Arg.Any<T>()).Returns(insertable);
            mockDb.Insertable(Arg.Any<List<T>>()).Returns(insertable);
        }

        public static void SetupUpdateable<T>(ISqlSugarClient mockDb, int affectedRows = 1) where T : class, new()
        {
            var updateable = Substitute.For<IUpdateable<T>>();
            updateable.ExecuteCommandAsync().Returns(Task.FromResult(affectedRows));
            
            // Support method chaining for SetColumns and Where
            updateable.SetColumns(Arg.Any<System.Linq.Expressions.Expression<Func<T, T>>>())
                .Returns(updateable);
            updateable.Where(Arg.Any<System.Linq.Expressions.Expression<Func<T, bool>>>())
                .Returns(updateable);
            
            mockDb.Updateable<T>().Returns(updateable);
            mockDb.Updateable(Arg.Any<T>()).Returns(updateable);
            mockDb.Updateable(Arg.Any<List<T>>()).Returns(updateable);
        }

        public static void SetupDeleteable<T>(ISqlSugarClient mockDb, int affectedRows = 1) where T : class, new()
        {
            var deleteable = Substitute.For<IDeleteable<T>>();
            deleteable.ExecuteCommandAsync().Returns(Task.FromResult(affectedRows));
            
            deleteable.Where(Arg.Any<System.Linq.Expressions.Expression<Func<T, bool>>>())
                .Returns(deleteable);
            
            mockDb.Deleteable<T>().Returns(deleteable);
            mockDb.Deleteable(Arg.Any<T>()).Returns(deleteable);
        }

        public static void SetupGroupByQuery<T, TResult>(
            ISqlSugarClient mockDb,
            List<TResult> groupResults) where T : class, new()
        {
            var queryable = Substitute.For<ISugarQueryable<T>>();
            
            queryable.Where(Arg.Any<System.Linq.Expressions.Expression<Func<T, bool>>>())
                .Returns(queryable);
            
            queryable.GroupBy(Arg.Any<System.Linq.Expressions.Expression<Func<T, object>>>())
                .Returns(Substitute.For<ISugarQueryable<T>>());
            
            // This is a simplified mock - in reality, GroupBy returns a different type
            // For our tests, we'll mock the final Select result
            var selectQueryable = Substitute.For<ISugarQueryable<TResult>>();
            selectQueryable.ToListAsync().Returns(Task.FromResult(groupResults));
            
            mockDb.Queryable<T>().Returns(queryable);
        }

        public static void VerifyInsertWasCalled<T>(ISqlSugarClient mockDb, T expectedEntity) where T : class, new()
        {
            mockDb.Received(1).Insertable(Arg.Is<T>(entity => 
                entity.Equals(expectedEntity)));
        }

        public static void VerifyUpdateWasCalled<T>(ISqlSugarClient mockDb) where T : class, new()
        {
            mockDb.Received(1).Updateable(Arg.Any<T>());
        }

        public static void VerifyQueryWasCalled<T>(ISqlSugarClient mockDb) where T : class
        {
            mockDb.Received().Queryable<T>();
        }
    }
}