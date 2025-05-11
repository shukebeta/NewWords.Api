# Database: NewWords Table: WordExplanations

 Field               | Type         | Null | Default | Comment
---------------------|--------------|------|---------|---------
 Id                  | bigint       | NO   |         |
 WordCollectionId    | bigint       | NO   |         |
 WordText            | varchar(255) | NO   |         |
 WordLanguage        | varchar(20)  | NO   |         |
 ExplanationLanguage | varchar(20)  | NO   |         |
 MarkdownExplanation | text         | YES  |         |
 Pronunciation       | varchar(512) | YES  |         |
 Definitions         | text         | YES  |         |
 Examples            | text         | YES  |         |
 CreatedAt           | bigint       | NO   |         |
 ProviderModelName   | varchar(100) | YES  |         |

## Indexes: 

 Key_name                      | Column_name         | Seq_in_index | Non_unique | Index_type | Visible
-------------------------------|---------------------|--------------|------------|------------|---------
 PRIMARY                       | Id                  |            1 |          0 | BTREE      | YES
 UQ_Words_Text_Lang_NativeLang | WordText            |            1 |          0 | BTREE      | YES
 UQ_Words_Text_Lang_NativeLang | WordLanguage        |            2 |          0 | BTREE      | YES
 UQ_Words_Text_Lang_NativeLang | ExplanationLanguage |            3 |          0 | BTREE      | YES
 WordCollection_ibfk_1         | WordCollectionId    |            1 |          1 | BTREE      | YES
