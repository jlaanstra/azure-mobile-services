/// <reference path="../mobileservices.intellisense.js" />

var responseHelper = require('../shared/responseHelper');
var conflicts = require('../shared/conflicts');

function del(id, user, request) {

    var requestVersion = request.parameters.version;
    //existing items must have a timestamp
    if (requestVersion === undefined) {
        request.respond(statusCodes.BAD_REQUEST,
        "delete operation must specify the version parameter");
        return;
    }

    var tableName = tables.current.getTableName();

    //only delete if false to safe an unnecessary changes
    var sqlUpdate = "UPDATE " + tableName + " SET isDeleted = 'True' WHERE id = ?";
    var sqlSelect = "SELECT * FROM " + tableName + " WHERE id = ?";

    var processResult = function (results) {

        if (!Array.isArray(results)) {
            results = [results];
        }

        results.map(function (item) {
            delete item.__version;
            tables.current.update(item, {
                systemProperties: ['*'],
                success: function () { },
                error: function (err) {
                    console.error("Error occurred. Details:", err);
                    request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
                }
            });
        });

        var nondeleted = [];
        var deleted = [];
        results.map(function (r) {
            if (!r.isDeleted) {
                nondeleted.push(r);
            } else {
                deleted.push(r.id);
            }
        });
        responseHelper.sendSuccessResponse(request, "", nondeleted, deleted, {});
    }

    mssql.query(sqlSelect, id, {
        success: function (results) {
            if (results.length > 0) {
                if (results[0].__version.toString('base64') === requestVersion) {
                    mssql.query(sqlUpdate, id, {
                        success: function () {
                            responseHelper.sendSuccessResponse(request, "", [], [id], {});
                        },
                        error: function (err) {
                            console.error("Error occurred. Details:", err);
                            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
                        }
                    });
                } else if (results[0].__version.toString('base64') > requestVersion) {
                    var newItem = {}
                    for (var item in results[0]) {
                        newItem[item] = results[0][item];
                    }
                    newItem.isDeleted = true;

                    processResult(conflicts.resolveConflictOnServer(results[0], newItem, conflicts.latestWriteWins));
                    //conflicts.resolveConflictOnClient(request, results[0], newItem);
                } else {
                    request.respond(statusCodes.BAD_REQUEST);
                }
            }
            else {
                responseHelper.sendSuccessResponse(request, "", [], [id], {});
            }
        },
        error: function (err) {
            console.error("Error occurred. Details:", err);
            request.respond(statusCodes.INTERNAL_SERVER_ERROR, err);
        }
    });
}