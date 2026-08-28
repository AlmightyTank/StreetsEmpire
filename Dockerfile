# Street Empire, as one image: the API with the built client inside it.
#
# One image rather than two on purpose. The client is a bundle of static files, not a service - giving
# it a container of its own would mean a second web server, a proxy in front of both, a CORS allowlist
# to keep in step, and two origins where Discord's callback can only ever be registered against one.
# Served from the API it is same-origin, and all four of those problems stop existing.

# ---- the client -------------------------------------------------------------------------------
FROM node:22-alpine AS client
WORKDIR /client

# Manifests first, so editing a component does not throw away the installed modules.
COPY Client/package.json Client/package-lock.json ./
RUN npm ci

COPY Client/ ./
# vite.config.ts reads the version from here, one directory up, so the build needs it in the image.
# After npm ci rather than before, so bumping a release does not re-install node_modules.
COPY VERSION /VERSION
RUN npm run build


# ---- the server -------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src

# Same trick: restore against the project files alone, so a code change does not re-fetch NuGet.
COPY StreetEmpire.sln ./
# Both are read while MSBuild evaluates, so they have to be here before restore rather than before
# publish. Without them MSBuild silently falls back and the image ships reporting a version nobody
# released - which is why the publish below asks for the strict check.
COPY VERSION Directory.Build.props ./
COPY Server/StreetEmpire.Api/StreetEmpire.Api.csproj Server/StreetEmpire.Api/
COPY Tests/StreetEmpire.Tests/StreetEmpire.Tests.csproj Tests/StreetEmpire.Tests/
RUN dotnet restore Server/StreetEmpire.Api/StreetEmpire.Api.csproj

COPY Server/ Server/

# The commit this was built from, stamped into the assembly so /api/health can answer "is the thing I
# deployed the thing running". It has to be passed in: the build context carries Server/ and not .git,
# so nothing in here could work it out. Empty when somebody builds by hand, which is honest - a local
# build genuinely is not any particular commit.
ARG GIT_SHA=""
RUN dotnet publish Server/StreetEmpire.Api/StreetEmpire.Api.csproj -c Release -o /app --no-restore \
    -p:StreetEmpireStrictVersion=true -p:SourceRevisionId=$GIT_SHA


# ---- what actually runs -----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is here for the healthcheck and nothing else. The runtime image ships without any HTTP client,
# and a healthcheck that cannot make a request is a healthcheck that always passes.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=server /app ./
# Into wwwroot, which is where the API looks for a client to serve.
COPY --from=client /client/dist ./wwwroot

# The one directory the app writes to: the data protection key ring. Created here and owned by the
# user that will run, because Docker takes a fresh named volume's ownership from the image directory
# it is mounted over - so the volume comes out writable rather than root's.
RUN useradd --system --uid 5000 --no-create-home streetempire \
    && mkdir -p /keys \
    && chown -R streetempire:streetempire /keys
USER streetempire

# 8080 rather than 80: a non-root user cannot bind a privileged port.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Against the app's own health endpoint, which is exempt from rate limiting and touches no database.
HEALTHCHECK --interval=30s --timeout=3s --start-period=45s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "StreetEmpire.Api.dll"]
