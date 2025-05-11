# Database: NewWords Table: Users

 Field                   | Type         | Null | Default | Comment
-------------------------|--------------|------|---------|---------
 Id                      | int          | NO   |         |
 Email                   | varchar(200) | NO   |         |
 Gravatar                | varchar(255) | YES  |         |
 Salt                    | varchar(64)  | NO   |         |
 PasswordHash            | varchar(200) | NO   |         |
 NativeLanguage          | varchar(20)  | NO   |         |
 CurrentLearningLanguage | varchar(20)  | NO   |         |
 CreatedAt               | bigint       | NO   |         |
 UpdatedAt               | bigint       | YES  |         |
 DeletedAt               | bigint       | YES  |         |

## Indexes: 

 Key_name | Column_name | Seq_in_index | Non_unique | Index_type | Visible
----------|-------------|--------------|------------|------------|---------
 PRIMARY  | Id          |            1 |          0 | BTREE      | YES
 Email    | Email       |            1 |          0 | BTREE      | YES
