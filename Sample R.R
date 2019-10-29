library(quantmod)
library(MASS)

#https://old.nasdaq.com/screening/companies-by-name.aspx?letter=0&exchange=nasdaq&render=download
#this is a download of all of the symbols and the associated companies
setwd("C:/Projects/Predictor")
allSymbols <- read.csv(file="companylist.csv",header=TRUE,sep=",")
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