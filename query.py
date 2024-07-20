import stardict
import os
import csv

output = ""

with open('processed.txt') as file:
    contents = file.read()
    list = contents.split(' ')
    csv = stardict.DictCsv("ecdict.csv")

    for word in list:
        try:
            dict = csv.query(word)
            output += word + " \\" + dict.get("phonetic") + "\\ " + dict.get("translation").replace("\n", " ") + "\n"
        except:
            continue

with open('output.txt', "w", encoding="utf-8") as file:
    file.write(output)