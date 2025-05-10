using Api.Framework.Models;
using NewWords.Api.Entities;
using SqlSugar;

namespace NewWords.Api.Services
{
    public class VocabularyService(ISqlSugarClient db, IUserService userService, ILogger<VocabularyService> logger)
        : IVocabularyService
    {
        public async Task<PageData<Word>> GetUserWordsAsync(long userId, int pageSize, int pageNumber)
        {
            RefAsync<int> totalCount = 0;
            var pagedWords = await db.Queryable<UserWordEntity>()
                .LeftJoin<Word>((uwe, w) => uwe.WordId == w.WordId)
                .Where(uwe => uwe.UserId == userId)
                .OrderBy(uwe => uwe.CreatedAt, OrderByType.Desc)
                .Select((uwe, w) => w)
                .ToPageListAsync(pageNumber, pageSize, totalCount);

            return new PageData<Word>
            {
                DataList = pagedWords,
                TotalCount = totalCount,
                PageIndex = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<Word> AddUserWordAsync(long userId, Word wordToAdd)
        {
            var userProfile = await userService.GetUserProfileAsync(userId);
            if (userProfile == null)
            {
                throw new Exception("User profile not found.");
            }
            var userLearningLanguage = userProfile.CurrentLearningLanguage;

            if (!string.Equals(wordToAdd.WordLanguage, userLearningLanguage, StringComparison.OrdinalIgnoreCase))
            {
                // This check is to prevent adding a word in a language different from the user's learning language,
                // unless it already exists in the WordCollection for their learning language (e.g. user error in input).
                // The frontend is expected to handle confirmation if this exception is thrown.
                var existingWordInLearningLanguage = await db.Queryable<WordCollection>()
                    .Where(wc => wc.WordText == wordToAdd.WordText && wc.Language == userLearningLanguage && wc.DeletedAt == null)
                    .AnyAsync();

                if (!existingWordInLearningLanguage)
                {
                     throw new ArgumentException($"The entered word's language ('{wordToAdd.WordLanguage}') does not match your learning language ('{userLearningLanguage}'). Please confirm the word and its language.");
                }
            }

            // If ExplanationLanguage is not provided or doesn't match user's native language, log a warning but proceed.
            // The primary purpose is to ensure an explanation language is set, defaulting to user's native language.
            if (string.IsNullOrWhiteSpace(wordToAdd.ExplanationLanguage)) {
                wordToAdd.ExplanationLanguage = userProfile.NativeLanguage;
            } else if (!string.Equals(wordToAdd.ExplanationLanguage, userProfile.NativeLanguage, StringComparison.OrdinalIgnoreCase)) {
                 logger.LogWarning($"Word explanation language '{wordToAdd.ExplanationLanguage}' for user {userId} does not match user's native language '{userProfile.NativeLanguage}'. Proceeding with provided explanation language.");
            }

            try
            {
                await db.Ado.BeginTranAsync();

                var existingWordExplanation = await db.Queryable<Word>()
                    .Where(w => w.WordText == wordToAdd.WordText &&
                                w.WordLanguage == wordToAdd.WordLanguage &&
                                w.ExplanationLanguage == wordToAdd.ExplanationLanguage)
                    .SingleAsync();

                int wordExplanationId;

                if (existingWordExplanation != null)
                {
                    wordExplanationId = existingWordExplanation.WordId;
                }
                else
                {
                    wordToAdd.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    wordExplanationId = await db.Insertable(wordToAdd).ExecuteReturnIdentityAsync();
                    wordToAdd.WordId = wordExplanationId;
                }

                var wordInCollection = await db.Queryable<WordCollection>()
                    .Where(wc => wc.WordText == wordToAdd.WordText && wc.Language == wordToAdd.WordLanguage && wc.DeletedAt == null)
                    .SingleAsync();

                if (wordInCollection != null)
                {
                    await db.Updateable<WordCollection>()
                        .SetColumns(it => new WordCollection { QueryCount = it.QueryCount + 1, UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() })
                        .Where(it => it.Id == wordInCollection.Id)
                        .ExecuteCommandAsync();
                }
                else
                {
                    var newCollectionWord = new WordCollection
                    {
                        WordText = wordToAdd.WordText,
                        Language = wordToAdd.WordLanguage,
                        QueryCount = 1,
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    await db.Insertable(newCollectionWord).ExecuteCommandAsync();
                }

                var userWordLink = await db.Queryable<UserWordEntity>()
                    .Where(uw => uw.UserId == userId && uw.WordId == wordExplanationId)
                    .SingleAsync();

                if (userWordLink == null)
                {
                    var newUserWord = new UserWordEntity
                    {
                        UserId = userId,
                        WordId = wordExplanationId,
                        Status = 0,
                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    await db.Insertable(newUserWord).ExecuteCommandAsync();
                }

                await db.Ado.CommitTranAsync();
                // Fetch the definitive state of the word explanation from the DB to return.
                return await db.Queryable<Word>().InSingleAsync(wordExplanationId);
            }
            catch (Exception ex)
            {
                await db.Ado.RollbackTranAsync();
                logger.LogError(ex, $"Error adding word for user {userId}: {wordToAdd.WordText}");
                throw;
            }
        }
    }
}
