# Database: NewWords Table: WordCollection

 Field      | Type         | Null | Default | Comment
------------|--------------|------|---------|---------
 Id         | bigint       | NO   |         |
 WordText   | varchar(255) | NO   |         |
 Language   | varchar(20)  | NO   |         |
 QueryCount | bigint       | NO   |         |
 CreatedAt  | bigint       | NO   |         |
 UpdatedAt  | bigint       | YES  |         |
 DeletedAt  | bigint       | YES  |         |

## Indexes: 

 Key_name | Column_name | Seq_in_index | Non_unique | Index_type | Visible
----------|-------------|--------------|------------|------------|---------
 PRIMARY  | Id          |            1 |          0 | BTREE      | YES
 WordText | WordText    |            1 |          0 | BTREE      | YES
 WordText | Language    |            2 |          0 | BTREE      | YES
