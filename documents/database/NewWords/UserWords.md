# Database: NewWords Table: UserWords

 Field             | Type   | Null | Default | Comment
-------------------|--------|------|---------|---------
 Id                | bigint | NO   |         |
 UserId            | int    | NO   |         |
 WordExplanationId | bigint | NO   |         |
 Status            | int    | NO   | 0       |
 CreatedAt         | bigint | NO   |         |

## Indexes: 

 Key_name                   | Column_name       | Seq_in_index | Non_unique | Index_type | Visible
----------------------------|-------------------|--------------|------------|------------|---------
 PRIMARY                    | Id                |            1 |          0 | BTREE      | YES
 UQ_UserWords_UserId_WordId | UserId            |            1 |          0 | BTREE      | YES
 UQ_UserWords_UserId_WordId | WordExplanationId |            2 |          0 | BTREE      | YES
 UserWords_ibfk_2           | WordExplanationId |            1 |          1 | BTREE      | YES
