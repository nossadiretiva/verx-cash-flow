FROM node:20-alpine AS build
WORKDIR /app

COPY package*.json ./
<<<<<<< HEAD
RUN npm ci --ignore-scripts
=======
RUN npm ci --omit=dev --ignore-scripts
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2

COPY . .
RUN npm run build

FROM node:20-alpine AS runtime
WORKDIR /app

<<<<<<< HEAD
RUN apk add --no-cache curl

COPY package*.json ./
RUN npm ci --omit=dev --ignore-scripts

=======
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

COPY --from=build /app/dist ./dist
<<<<<<< HEAD
=======
COPY --from=build /app/node_modules ./node_modules
>>>>>>> 76cf6c5d4e4f8af03b387c6fe57874ffec4b56d2

EXPOSE 8081
ENV NODE_ENV=production

CMD ["node", "dist/main"]
