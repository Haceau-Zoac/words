# Words

获取文件或网页中的所有单词；生成单词表。

通过 `words`，你可以将任意 txt/pdf 文件或网站页面中的英文单词提取出来。

词典来源：[ECDICT](https://github.com/skywind3000/ECDICT)

## 获取单词

`words` 可以胜任这一工作。

- 参数 1：文件类型，可以是 `txt`、`pdf`、`url` 中的任何一个。
- 参数 2：文件路径/网址。
- 可选过滤器：`-f` + 文件名，例如 `-ffilter.txt` 就是将 `filter.txt` 作为过滤器，过滤得到单词中与 `filter.txt` 单词中相重复的单词。
- 可选排序：若使用了 `-s` 参数，则将会对单词进行排序，否则不会。

## 制作单词表
将 `ecdict.csv`、`query.py`、`stardict.py`、`gen.bat` 与 `words` 放在同一目录下，调用 `gen.bat`，生成 `output.txt`即为单词表。

`get.bat` 的参数同 `words`。