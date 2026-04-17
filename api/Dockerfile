FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY api/ScribAi.Api.csproj api/
RUN dotnet restore api/ScribAi.Api.csproj
COPY api/ api/
RUN dotnet publish api/ScribAi.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    tesseract-ocr \
    tesseract-ocr-spa \
    tesseract-ocr-eng \
    libleptonica-dev \
    libtesseract-dev \
    libgdiplus \
    libc6-dev \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata
COPY --from=build /app ./

RUN mkdir -p /app/tessdata && \
    cp -r /usr/share/tesseract-ocr/5/tessdata/*.traineddata /app/tessdata/ 2>/dev/null || \
    cp -r /usr/share/tesseract-ocr/4.00/tessdata/*.traineddata /app/tessdata/ 2>/dev/null || true

ENV ASPNETCORE_URLS=http://+:8080
ENV Processing__TesseractDataPath=/app/tessdata

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "ScribAi.Api.dll"]
