/// <reference path="mobileservices.intellisense.js" />

var responseHelper = require('../shared/responseHelper');
var conflicts = require('../shared/conflicts');
var async = require('async');

function update(item, user, request) {

    //existing items must have a timestamp
    if (item.__version === undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "update operation must have __version");
        return;
    }
    //item cannot set isDeleted
    if (item.isDeleted !== undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "item cannot set isDeleted, isDeleted is a reserved column name");
        return;
    }

    //configurable conflict resolution
    var resolveStrategy = request.parameters.resolveStrategy;
    if (resolveStrategy === undefined) {
        resolveStrategy = "latestWriteWins";
    }

    var processResult = function (results) {

        if (!Array.isArray(results)) {
            results = [results];
        }

        var nondeleted = [];
        var deleted = [];
        results.map(function (r) {
            if (!r.isDeleted) {
                nondeleted.push(r);
            } else {
                deleted.push(r.id);
            }
        });
        responseHelper.sendSuccessResponse(request, "", nondeleted, deleted, { conflictResolved: resolveStrategy });
    }

    var processResolution = function (results) {
        if (!Array.isArray(results)) {
            results = [results];
        }
        
        function updateItem(item, callback) {
            delete item.__version;
            tables.current.update(item, {
                systemProperties: ['*'],
                success: function () {
                    callback(null, item);
                },
                error: function (err) {
                    callback(err, null);
                }
            });
        }

        function done(err, updatedResults) {
            if (err !== null) {
                console.error("Error occurred. Details:", err);
                request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
            }
            else {
                processResult(updatedResults);
            }
        }

        async.map(results, updateItem, done);
    }

    request.execute({
        systemProperties: ['*'],
        success: function () {
            processResult(item);
        },
        conflict: function (serverItem) {
            item.isDeleted = false;

            //configurable conflict resolution
            if (resolveStrategy === "client") {
                conflicts.resolveConflictOnClient(request, serverItem, item);
            }
            else {
                processResolution(conflicts.resolveConflictOnServer(serverItem, item, conflicts[resolveStrategy]));
            }
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });
}