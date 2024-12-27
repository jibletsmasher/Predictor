# To run this file, select all (CTRL+a), then CTRL+Enter
# Had to reinstall this for things to work
#install.packages('quantmod', type="source")

library(quantmod)
library(MASS)

# Use this line to test to ensure the quantmod package is working
# After running the c# code, find the log file for the most recent run and copy/paste the last line into here
displaySymbols <- c("AFCG","AGEN","AGMH","APPS","BCAX","BILI","BJK","BLBD","BLLD","CLSD","CMCT","CMND","CSTL","CTSO","ENPH","ETEC","EVMT","HTHT","HUBC","INSE","JZXN","LAZR","LVLU","NCNA","NFXS","NISN","NMTC","OPT","PBBK","PDBC","RAPT","RCKT","RILYL","SCVL","SSP","TOP","TRNS","UPB","VCSH","VYGR","WETH","WLDS","YTRA")
print(displaySymbols)
for(displaySymbol in displaySymbols)
{
  symbols <- getSymbols(displaySymbol, src="yahoo", from=(as.Date(as.POSIXlt(Sys.time()))-360), auto.assign=FALSE)
  symbols <- as.data.frame(symbols)
  chartSeries(symbols, name = toString(displaySymbol))
}

setwd("C:/Projects/Predictor")
# Get list of symbols from https://raw.githubusercontent.com/datasets/nasdaq-listings/refs/heads/main/data/nasdaq-listed.csv
# when updating the list, go to notepad++ and do a find replace regular expression for:
#   - find: ([A-Z]+),([^ ])
#   - replace: \"\1\",\2
# Then fix the columns names, so they are correct csv format
# ^ this is to get quotes around all of the symbol tickers and is needed
# do a find replace regular expression for:
# 
#this is a download of all of the symbols and the associated companies
allSymbols <- read.csv(file="companylistv2.csv",header=TRUE,sep=",")
setwd("C:/Projects/Predictor/Symbols")
for(row in 1:nrow(allSymbols))
{
	tryCatch(
	{
		symbolData <- getSymbols(toString(allSymbols[row,1]), src="yahoo", from=(as.Date(as.POSIXlt(Sys.time()))-91), auto.assign=FALSE)
		na.omit(symbolData)
		writeableData <- c(as.character(index(symbolData)[1]), symbolData[1 ,1], symbolData[1 ,2], 
						symbolData[1 ,3], symbolData[1 ,4], symbolData[1 ,5], symbolData[1 ,6])
		for(dataRow in 2:nrow(symbolData))
		{
			writeableData <- rbind(writeableData, c(as.character(index(symbolData)[dataRow]), symbolData[dataRow ,1], symbolData[dataRow ,2], 
										symbolData[dataRow ,3], symbolData[dataRow ,4], symbolData[dataRow ,5], symbolData[dataRow ,6]))
		}
		#can't figure out how the fuck this works: symbolData <- cbind(index(symbolData), symbolData)
		write.csv(writeableData, file=paste(toString(allSymbols[row,1]),".csv"))
		#don't know what package this is part of: fwrite(symbolData, file=paste(toString(allSymbols[row,1]),".csv"), sep=",")
	},
	error=function(cond) 
	{
		message("Something went wrong with symbol ", toString(allSymbols[row,1]))
		message()
		message(cond)
		message()
		message()
	},
	warning=function(cond)
	{
		message(cond)
		message()
		message()
	})
}

