# Database: NewWords Table: UserSettings

 Field        | Type         | Null | Default | Comment
--------------|--------------|------|---------|---------
 Id           | bigint       | NO   |         |
 UserId       | bigint       | NO   |         |
 SettingName  | varchar(255) | NO   |         |
 SettingValue | text         | NO   |         |
 CreatedAt    | bigint       | NO   |         |
 DeletedAt    | bigint       | YES  |         |
 UpdatedAt    | bigint       | YES  |         |

## Indexes: 

 Key_name | Column_name | Seq_in_index | Non_unique | Index_type | Visible
----------|-------------|--------------|------------|------------|---------
 PRIMARY  | Id          |            1 |          0 | BTREE      | YES
