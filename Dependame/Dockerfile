# Stage 1: Build
# https://devblogs.microsoft.com/dotnet/improving-multiplatform-container-support/
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS builder
ARG PROJECT_FILE="Dependame.csproj"
ARG TARGETARCH
ARG BUILDPLATFORM

WORKDIR /app
COPY . .
RUN dotnet restore $PROJECT_FILE -r linux-musl-$TARGETARCH
RUN dotnet publish $PROJECT_FILE \
    -c Release \
    -r linux-musl-$TARGETARCH \
    -o /app/publish \
    --self-contained true \
    /p:PublishSingleFile=true

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine

# Globalization support
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV LANG=en_US.UTF-8
ENV LC_ALL=en_US.UTF-8

RUN apk add --no-cache icu-data-full icu-libs gcompat tzdata

WORKDIR /app
COPY --from=builder /app/publish .

# Set permissions
RUN chmod +x /app/*

# Create entrypoint symlink (handles dynamic binary name)
RUN ln -s $(ls /app | grep -v '*.pdb' | head -1) /app/__entrypoint

ENTRYPOINT ["/app/__entrypoint"]
