# Database: NewWords Table: LlmConfigurations

 Field       | Type          | Null | Default | Comment
-------------|---------------|------|---------|---------
 LlmConfigId | int           | NO   |         |
 ModelName   | varchar(100)  | NO   |         |
 DisplayName | varchar(100)  | YES  |         |
 IsEnabled   | tinyint(1)    | NO   | 1       |
 ApiKey      | varchar(1024) | YES  |         |
 CreatedAt   | bigint        | NO   |         |

## Indexes: 

 Key_name  | Column_name | Seq_in_index | Non_unique | Index_type | Visible
-----------|-------------|--------------|------------|------------|---------
 PRIMARY   | LlmConfigId |            1 |          0 | BTREE      | YES
 ModelName | ModelName   |            1 |          0 | BTREE      | YES
