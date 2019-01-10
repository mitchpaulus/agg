# agg

This tool is designed to handle the following scenario. 

You are given a delimited, text, data file with timestamped data. For
example, something like this data coming in at 30 minute intervals:

```
2018-01-01T00:00	1
2018-01-01T00:30	2
2018-01-01T01:00	3
... and so on ...
```

Now you want to get the max value of your field per day, month, and
year. You can use `agg` like

```
agg --agg max --period day file.txt
```

to get the following out to standard output.

```
2018-01-01  3
2018-01-02  5
2018-01-03  4
... and so on ...
```

What can `agg` all do? It can get you the sum, mean, count, max, or min
of the fields in your data file.

You can also pipe the data into `agg` so you can easily use other tools
to preprocess the data set. For example, you may want to clear all
thousands commas in your data file prior to aggregating.

```
sed 's/,//g' file.txt | agg
```

## Defaults

By default, `agg` will sum your data to daily totals. The default
delimiter is whitespace. You can modify the delimiter using the `-d` or
`--delim` option. So for a CSV file:

```
agg --delim "," file.txt
```

`agg` will start with the first line -- If you have header lines, you
can skip them using the `-n` or `--skip` option. (Although the more Unix
thing to do would be use tail :D)
