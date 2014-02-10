﻿// The MIT License (MIT)

// Copyright (c) 2014 Ben Abelshausen

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using GTFS.Entities;
using GTFS.Entities.Enumerations;
using GTFS.Exceptions;
using GTFS.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GTFS
{
    /// <summary>
    /// A GTFS reader.
    /// </summary>
    public class GTFSReader<T> where T : IGTFSFeed, new()
    {
        /// <summary>
        /// Reads the specified GTFS source into a new GTFS feed object.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public T Read(IEnumerable<IGTFSSourceFile> source)
        {
            return this.Read(new T(), source);
        }

        /// <summary>
        /// Reads the specified GTFS source into the given GTFS feed object.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public T Read(T feed, IEnumerable<IGTFSSourceFile> source)
        {
            // check if all required files are present.
            foreach(var file in this.GetRequiredFiles())
            {
                if(!source.Any(x => x.Name.Equals(file)))
                { // oeps, file was found found!
                    throw new GTFSRequiredFileMissingException(file);
                }
            }

            // read files one-by-one and in the correct order based on the dependency tree.
            var readFiles = new HashSet<string>();
            var dependencyTree = this.GetDependencyTree();
            while(readFiles.Count < source.Count())
            {
                // select a new file based on the dependency tree.
                IGTFSSourceFile selectedFile = null;
                foreach(var file in source)
                {
                    if (!readFiles.Contains(file.Name))
                    { // file has not been read yet!
                        HashSet<string> dependencies = null;
                        if (!dependencyTree.TryGetValue(file.Name, out dependencies))
                        { // there is no entry in the dependency tree, file is independant.
                            selectedFile = file;
                            break;
                        }
                        else
                        { // file depends on other file, check if they have been read already.
                            if (dependencies.All(x => readFiles.Contains(x)))
                            { // all dependencies have been read.
                                selectedFile = file;
                                break;
                            }
                        }
                    }
                }

                // check if there is a next file.
                if(selectedFile == null)
                {
                    throw new Exception("Could not select a next file based on the current dependency tree and the current file list.");
                }

                // read the file.
                this.Read(selectedFile, feed);
                readFiles.Add(selectedFile.Name);
            }
            return feed;            
        }

        /// <summary>
        /// Returns a list of all required files.
        /// </summary>
        /// <returns></returns>
        public virtual HashSet<string> GetRequiredFiles()
        {
            var files = new HashSet<string>();
            files.Add("agency");
            files.Add("stops");
            files.Add("routes");
            files.Add("trips");
            files.Add("stop_times");
            files.Add("calendar");
            return files;
        }

        /// <summary>
        /// Returns the file dependency-tree.
        /// </summary>
        /// <returns></returns>
        public virtual Dictionary<string, HashSet<string>> GetDependencyTree()
        {
            var dependencyTree = new Dictionary<string, HashSet<string>>();

            // fare_rules => (routes)
            var dependencies = new HashSet<string>();
            dependencies.Add("routes");
            dependencyTree.Add("fare_rules", dependencies);

            // frequencies => (trips)
            dependencies = new HashSet<string>();
            dependencies.Add("trips");
            dependencyTree.Add("frequencies", dependencies);

            // routes => (agencies)
            dependencies = new HashSet<string>();
            dependencies.Add("agency");
            dependencyTree.Add("routes", dependencies);

            // stop_times => (trips)
            dependencies = new HashSet<string>();
            dependencies.Add("trips");
            dependencyTree.Add("stop_times", dependencies);

            // trips => (routes)
            dependencies = new HashSet<string>();
            dependencies.Add("routes");
            dependencyTree.Add("trips", dependencies);

            return dependencyTree;
        }

        /// <summary>
        /// A delegate for parsing methods per entity.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        protected delegate TEntity EntityParseDelegate<TEntity>(T feed, GTFSSourceFileHeader header, string[] data)
            where TEntity : GTFSEntity;

        /// <summary>
        /// A delegate to add entities.
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        protected delegate void EntityAddDelegate<TEntity>(TEntity entity);

        /// <summary>
        /// Reads the given file and adds the result to the feed.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="feed"></param>
        protected virtual void Read(IGTFSSourceFile file, T feed)
        {
            switch(file.Name.ToLower())
            {
                case "agency":
                    this.Read<Agency>(file, feed, this.ParseAgency, feed.AddAgency);
                    break;
                case "calendar":
                    this.Read<Calendar>(file, feed, this.ParseCalender, feed.AddCalendar);
                    break;
                case "calendar_dates":
                    this.Read<CalendarDate>(file, feed, this.ParseCalendarDate, feed.AddCalendarDate);
                    break;
                case "fare_attributes":
                    this.Read<FareAttribute>(file, feed, this.ParseFareAttribute, feed.AddFareAttribute);
                    break;
                case "fare_rules":
                    this.Read<FareRule>(file, feed, this.ParseFareRule, feed.AddFareRule);
                    break;
                case "feed_info":
                    this.Read<FeedInfo>(file, feed, this.ParseFeedInfo, feed.AddFeedInfo);
                    break;
                case "routes":
                    this.Read<Route>(file, feed, this.ParseRoute, feed.AddRoute);
                    break;
                case "shapes":
                    this.Read<Shape>(file, feed, this.ParseShape, feed.AddShape);
                    break;
                case "stops":
                    this.Read<Stop>(file, feed, this.ParseStop, feed.AddStop);
                    break;
                case "stop_times":
                    this.Read<StopTime>(file, feed, this.ParseStopTime, feed.AddStopTime);
                    break;
                case "trips":
                    this.Read<Trip>(file, feed, this.ParseTrip, feed.AddTrip);
                    break;
                case "frequencies":
                    this.Read<Frequency>(file, feed, this.ParseFrequency, feed.AddFrequency);
                    break;
            }
        }

        /// <summary>
        /// Reads the agency file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="list"></param>
        private void Read<TEntity>(IGTFSSourceFile file, T feed, EntityParseDelegate<TEntity> parser, EntityAddDelegate<TEntity> addDelegate)
            where TEntity : GTFSEntity
        {
            // enumerate all lines.
            var enumerator = file.GetEnumerator();
            if(!enumerator.MoveNext())
            { // there is no data, and if there is move to the columns.
                return;
            }

            // read the header.
            var header = new GTFSSourceFileHeader(file.Name, enumerator.Current);

            // read fields.
            while (enumerator.MoveNext())
            {
                addDelegate.Invoke(parser.Invoke(feed, header, enumerator.Current));
            }
        }

        /// <summary>
        /// Parses an agency row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Agency ParseAgency(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "agency_id");
            this.CheckRequiredField(header, header.Name, "agency_name");
            this.CheckRequiredField(header, header.Name, "agency_url");
            this.CheckRequiredField(header, header.Name, "agency_timezone");

            // parse/set all fields.
            Agency agency = new Agency();
            for(int idx = 0; idx < data.Length; idx++)
            {
                this.ParseAgencyField(header, agency, header.GetColumn(idx), data[idx]);
            }
            return agency;
        }

        /// <summary>
        /// Parses an agency field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="name"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseAgencyField(GTFSSourceFileHeader header, Agency agency, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "agency_id":
                    agency.Id = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "agency_name":
                    agency.Name = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "agency_lang":
                    agency.LanguageCode = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "agency_phone":
                    agency.Phone = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "agency_timezone":
                    agency.Timezone = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "agency_url":
                    agency.URL = this.ParseFieldString(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a calendar row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Calendar ParseCalender(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "service_id");
            this.CheckRequiredField(header, header.Name, "monday");
            this.CheckRequiredField(header, header.Name, "tuesday");
            this.CheckRequiredField(header, header.Name, "wednesday");
            this.CheckRequiredField(header, header.Name, "thursday");
            this.CheckRequiredField(header, header.Name, "friday");
            this.CheckRequiredField(header, header.Name, "saturday");
            this.CheckRequiredField(header, header.Name, "sunday");
            this.CheckRequiredField(header, header.Name, "start_date");
            this.CheckRequiredField(header, header.Name, "end_date");

            // parse/set all fields.
            Calendar calendar = new Calendar();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseCalendarField(feed, header, calendar, header.GetColumn(idx), data[idx]);
            }
            return calendar;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="route"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseCalendarField(T feed, GTFSSourceFileHeader header, Calendar calendar, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "service_id":
                    calendar.ServiceId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "monday":
                    calendar.Monday = this.ParseFieldBool(header.Name, fieldName, value).Value;
                    break;
                case "tuesday":
                    calendar.Tuesday = this.ParseFieldBool(header.Name, fieldName, value).Value;
                    break;
                case "wednesday":
                    calendar.Wednesday = this.ParseFieldBool(header.Name, fieldName, value).Value;
                    break;
                case "thursday":
                    calendar.Thursday = this.ParseFieldBool(header.Name, fieldName, value).Value;
                    break;
                case "friday":
                    calendar.Friday = this.ParseFieldBool(header.Name, fieldName, value).Value;
                    break;
                case "saturday":
                    calendar.Saturday = this.ParseFieldBool(header.Name, fieldName, value).Value;
                    break;
                case "sunday":
                    calendar.Sunday = this.ParseFieldBool(header.Name, fieldName, value).Value;
                    break;
                case "start_date":
                    calendar.StartDate = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "end_date":
                    calendar.EndDate = this.ParseFieldString(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a calendar date row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual CalendarDate ParseCalendarDate(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "service_id");
            this.CheckRequiredField(header, header.Name, "date");
            this.CheckRequiredField(header, header.Name, "exception_type");

            // parse/set all fields.
            CalendarDate calendarDate = new CalendarDate();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseCalendarDateField(feed, header, calendarDate, header.GetColumn(idx), data[idx]);
            }
            return calendarDate;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="route"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseCalendarDateField(T feed, GTFSSourceFileHeader header, CalendarDate calendarDate, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "service_id":
                    calendarDate.ServiceId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "date":
                    calendarDate.Date = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "exception_type":
                    calendarDate.ExceptionType = this.ParseFieldExceptionType(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a fare attribute row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual FareAttribute ParseFareAttribute(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "fare_id");
            this.CheckRequiredField(header, header.Name, "price");
            this.CheckRequiredField(header, header.Name, "currency_type");
            this.CheckRequiredField(header, header.Name, "payment_method");
            this.CheckRequiredField(header, header.Name, "transfers");

            // parse/set all fields.
            FareAttribute fareAttribute = new FareAttribute();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseFareAttributeField(feed, header, fareAttribute, header.GetColumn(idx), data[idx]);
            }
            return fareAttribute;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="trip"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseFareAttributeField(T feed, GTFSSourceFileHeader header, FareAttribute fareAttribute, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "fare_id":
                    fareAttribute.FareId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "price":
                    fareAttribute.Price = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "currency_type":
                    fareAttribute.CurrencyType = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "payment_method":
                    fareAttribute.PaymentMethod = this.ParseFieldPaymentMethodType(header.Name, fieldName, value);
                    break;
                case "transfers":
                    fareAttribute.Transfers = this.ParseFieldUInt(header.Name, fieldName, value);
                    break;
                case "transfer_duration":
                    fareAttribute.TransferDuration = this.ParseFieldString(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a fare rule row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual FareRule ParseFareRule(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "fare_id");

            // parse/set all fields.
            FareRule fareRule = new FareRule();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseFareRuleField(feed, header, fareRule, header.GetColumn(idx), data[idx]);
            }
            return fareRule;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="trip"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseFareRuleField(T feed, GTFSSourceFileHeader header, FareRule fareRule, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "fare_id":
                    fareRule.FareId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "route_id":
                    fareRule.RouteId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "origin_id":
                    fareRule.OriginId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "destination_id":
                    fareRule.DestinationId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "contains_id":
                    fareRule.ContainsId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a feed info row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual FeedInfo ParseFeedInfo(T feed, GTFSSourceFileHeader header, string[] data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Parses a frequency row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Frequency ParseFrequency(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "trip_id");
            this.CheckRequiredField(header, header.Name, "start_time");
            this.CheckRequiredField(header, header.Name, "end_time");
            this.CheckRequiredField(header, header.Name, "headway_secs");

            // parse/set all fields.
            Frequency frequency = new Frequency();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseFrequencyField(feed, header, frequency, header.GetColumn(idx), data[idx]);
            }
            return frequency;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="route"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseFrequencyField(T feed, GTFSSourceFileHeader header, Frequency frequency, string fieldName, string value)
        {
            this.CheckRequiredField(header, header.Name, "trip_id");
            this.CheckRequiredField(header, header.Name, "start_time");
            this.CheckRequiredField(header, header.Name, "end_time");
            this.CheckRequiredField(header, header.Name, "headway_secs");
            switch (fieldName)
            {
                case "trip_id":
                    frequency.TripId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "start_time":
                    frequency.StartTime = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "end_time":
                    frequency.EndTime = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "headway_secs":
                    frequency.HeadwaySecs = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "exact_times":
                    frequency.ExactTimes = this.ParseFieldBool(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a route row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Route ParseRoute(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "route_id");
            this.CheckRequiredField(header, header.Name, "agency_id");
            this.CheckRequiredField(header, header.Name, "route_short_name");
            this.CheckRequiredField(header, header.Name, "route_long_name");
            this.CheckRequiredField(header, header.Name, "route_desc");
            this.CheckRequiredField(header, header.Name, "route_type");

            // parse/set all fields.
            Route route = new Route();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseRouteField(feed, header, route, header.GetColumn(idx), data[idx]);
            }
            return route;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="route"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseRouteField(T feed, GTFSSourceFileHeader header, Route route, string fieldName, string value)
        {
            switch (fieldName)
            {            
                case "route_id":
                    route.Id = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "agency_id":
                    route.AgencyId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "route_short_name":
                    route.ShortName = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "route_long_name":
                    route.LongName= this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "route_desc":
                    route.Description = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "route_type":
                    route.Type = this.ParseFieldRouteType(header.Name, fieldName, value);
                    break;
                case "route_url":
                    route.Url = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "route_color":
                    route.Color = this.ParseFieldColor(header.Name, fieldName, value);
                    break;
                case "route_text_color":
                    route.TextColor = this.ParseFieldColor(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a shapte row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Shape ParseShape(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "shape_id");
            this.CheckRequiredField(header, header.Name, "shape_pt_lat");
            this.CheckRequiredField(header, header.Name, "shape_pt_lon");
            this.CheckRequiredField(header, header.Name, "shape_pt_sequence");

            // parse/set all fields.
            Shape shape = new Shape();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseShapeField(feed, header, shape, header.GetColumn(idx), data[idx]);
            }
            return shape;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="shape"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseShapeField(T feed, GTFSSourceFileHeader header, Shape shape, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "shape_id":
                    shape.Id = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "shape_pt_lat":
                    shape.Latitude = this.ParseFieldDouble(header.Name, fieldName, value).Value;
                    break;
                case "shape_pt_lon":
                    shape.Longitude = this.ParseFieldDouble(header.Name, fieldName, value).Value;
                    break;
                case "shape_pt_sequence":
                    shape.Sequence = this.ParseFieldUInt(header.Name, fieldName, value).Value;
                    break;
                case "shape_dist_traveled":
                    shape.DistanceTravelled = this.ParseFieldDouble(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a stop row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Stop ParseStop(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "stop_id");
            this.CheckRequiredField(header, header.Name, "stop_name");
            this.CheckRequiredField(header, header.Name, "stop_lat");
            this.CheckRequiredField(header, header.Name, "stop_lon");

            // parse/set all fields.
            Stop stop = new Stop();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseStopField(feed, header, stop, header.GetColumn(idx), data[idx]);
            }
            return stop;
        }

        /// <summary>
        /// Parses a stop field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="stop"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseStopField(T feed, GTFSSourceFileHeader header, Stop stop, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "stop_id":
                    stop.Id = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "stop_code":
                    stop.Code = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "stop_name":
                    stop.Name = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "stop_desc":
                    stop.Description = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "stop_lat":
                    stop.Latitude = this.ParseFieldDouble(header.Name, fieldName, value).Value;
                    break;
                case "stop_lon":
                    stop.Longitude = this.ParseFieldDouble(header.Name, fieldName, value).Value;
                    break;
                case "zone_id":
                    stop.Zone = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "stop_url":
                    stop.Url = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "location_type":
                    stop.LocationType = this.ParseFieldLocationType(header.Name, fieldName, value);
                    break;
                case "parent_station":
                    stop.ParentStation = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "stop_timezone":
                    stop.Timezone = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case " wheelchair_boarding ":
                    stop.WheelchairBoarding = this.ParseFieldString(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a stop time row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual StopTime ParseStopTime(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "trip_id");
            this.CheckRequiredField(header, header.Name, "arrival_time");
            this.CheckRequiredField(header, header.Name, "departure_time");
            this.CheckRequiredField(header, header.Name, "stop_id");
            this.CheckRequiredField(header, header.Name, "stop_sequence");
            this.CheckRequiredField(header, header.Name, "stop_id");
            this.CheckRequiredField(header, header.Name, "stop_id");

            // parse/set all fields.
            StopTime stopTime = new StopTime();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseStopTimeField(feed, header, stopTime, header.GetColumn(idx), data[idx]);
            }
            return stopTime;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="trip"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseStopTimeField(T feed, GTFSSourceFileHeader header, StopTime stopTime, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "trip_id":
                    stopTime.TripId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "arrival_time":
                    stopTime.ArrivalTime = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "departure_time":
                    stopTime.DepartureTime = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "stop_id":
                    stopTime.StopId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "stop_sequence":
                    stopTime.StopSequence = this.ParseFieldUInt(header.Name, fieldName, value).Value;
                    break;
                case "stop_headsign":
                    stopTime.StopHeadsign = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "pickup_type":
                    stopTime.PickupType = this.ParseFieldPickupType(header.Name, fieldName, value);
                    break;
                case "drop_off_type":
                    stopTime.DropOffType = this.ParseFieldDropOffType(header.Name, fieldName, value);
                    break;
                case "shape_dist_traveled":
                    stopTime.ShapeDistTravelled = this.ParseFieldString(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Parses a transfer row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Transfer ParseTransfer(T feed, GTFSSourceFileHeader header, string[] data)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Parses a trip row.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual Trip ParseTrip(T feed, GTFSSourceFileHeader header, string[] data)
        {
            // check required fields.
            this.CheckRequiredField(header, header.Name, "trip_id");
            this.CheckRequiredField(header, header.Name, "route_id");
            this.CheckRequiredField(header, header.Name, "service_id");
            this.CheckRequiredField(header, header.Name, "shape_pt_sequence");

            // parse/set all fields.
            Trip trip = new Trip();
            for (int idx = 0; idx < data.Length; idx++)
            {
                this.ParseTripField(feed, header, trip, header.GetColumn(idx), data[idx]);
            }
            return trip;
        }

        /// <summary>
        /// Parses a route field.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="trip"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        protected virtual void ParseTripField(T feed, GTFSSourceFileHeader header, Trip trip, string fieldName, string value)
        {
            switch (fieldName)
            {
                case "trip_id":
                    trip.Id = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "route_id":
                    trip.RouteId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "service_id":
                    trip.ServiceId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "trip_headsign":
                    trip.Headsign = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "trip_short_name":
                    trip.ShortName = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "direction_id":
                    trip.Direction = this.ParseFieldDirectionType(header.Name, fieldName, value);
                    break;
                case "block_id":
                    trip.BlockId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "shape_id":
                    trip.ShapeId = this.ParseFieldString(header.Name, fieldName, value);
                    break;
                case "wheelchair_accessible":
                    trip.AccessibilityType = this.ParseFieldAccessibilityType(header.Name, fieldName, value);
                    break;
            }
        }

        /// <summary>
        /// Checks if a required field is actually in the header.
        /// </summary>
        /// <param name="header"></param>
        /// <param name="name"></param>
        /// <param name="column"></param>
        protected virtual void CheckRequiredField(GTFSSourceFileHeader header, string name, string column)
        {
            if(!header.HasColumn(column))
            {
                throw new GTFSRequiredFieldMissingException(name, column);
            }
        }

        /// <summary>
        /// Parses a string-field.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        protected virtual string ParseFieldString(string name, string fieldName, string value)
        {
            // throw new GTFSParseException(name, fieldName, value);
            return value;
        }

        /// <summary>
        /// Parses a color field into an argb value.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual int? ParseFieldColor(string name, string fieldName, string value)
        {
            if(string.IsNullOrWhiteSpace(value))
            { // detect empty strings.
                return null;
            }

            int red = -1;
            int green = -1;
            int blue = -1;
            int alpha = 255;

            if(value.Length == 7)
            {
                try
                {
                    // a pre-defined RGB value.
                    string rString = value.Substring(1, 2);
                    string gString = value.Substring(3, 2);
                    string bString = value.Substring(5, 2);

                    red = int.Parse(rString);
                    green = int.Parse(gString);
                    blue = int.Parse(bString);
                }
                catch(Exception ex)
                {// hmm, some unknow exception, field not in correct format, give inner exception as a clue.
                    throw new GTFSParseException(name, fieldName, value, ex);
                }
            }
            else
            { // hmm, what kind of string is this going to be? if it is a color, augment the parser.
                throw new GTFSParseException(name, fieldName, value);
            }

            try
            {
                if ((alpha > 255) || (alpha < 0))
                {
                    // alpha out of range!
                    throw new ArgumentOutOfRangeException("alpha", "Value has to be in the range 0-255!");
                }
                if ((red > 255) || (red < 0))
                {
                    // red out of range!
                    throw new ArgumentOutOfRangeException("red", "Value has to be in the range 0-255!");
                }
                if ((green > 255) || (green < 0))
                {
                    // green out of range!
                    throw new ArgumentOutOfRangeException("green", "Value has to be in the range 0-255!");
                }
                if ((blue > 255) || (blue < 0))
                {
                    // red out of range!
                    throw new ArgumentOutOfRangeException("blue", "Value has to be in the range 0-255!");
                }
                return (int)((uint)alpha << 24) + (red << 16) + (green << 8) + blue;
            }
            catch (Exception ex)
            {// hmm, some unknow exception, field not in correct format, give inner exception as a clue.
                throw new GTFSParseException(name, fieldName, value, ex);
            }
        }

        /// <summary>
        /// Parses a route-type field.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual RouteType ParseFieldRouteType(string name, string fieldName, string value)
        {
            //0 - Tram, Streetcar, Light rail. Any light rail or street level system within a metropolitan area.
            //1 - Subway, Metro. Any underground rail system within a metropolitan area.
            //2 - Rail. Used for intercity or long-distance travel.
            //3 - Bus. Used for short- and long-distance bus routes.
            //4 - Ferry. Used for short- and long-distance boat service.
            //5 - Cable car. Used for street-level cable cars where the cable runs beneath the car.
            //6 - Gondola, Suspended cable car. Typically used for aerial cable cars where the car is suspended from the cable.
            //7 - Funicular. Any rail system designed for steep inclines.

            switch(value)
            {
                case "0":
                    return RouteType.Tram;
                case "1":
                    return RouteType.SubwayMetro;
                case "2":
                    return RouteType.Rail;
                case "3":
                    return RouteType.Bus;
                case "4":
                    return RouteType.Ferry;
                case "5":
                    return RouteType.CableCar;
                case "6":
                    return RouteType.Gondola;
                case "7":
                    return RouteType.Funicular;
            }
            throw new GTFSParseException(name, fieldName, value);
        }


        /// <summary>
        /// Parses an exception-type field.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual ExceptionType ParseFieldExceptionType(string name, string fieldName, string value)
        {
            //A value of 1 indicates that service has been added for the specified date.
            //A value of 2 indicates that service has been removed for the specified date.

            switch (value)
            {
                case "1":
                    return ExceptionType.Added;
                case "2":
                    return ExceptionType.Removed;
            }
            throw new GTFSParseException(name, fieldName, value);
        }

        /// <summary>
        /// Parses a payment-method type field.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual PaymentMethodType ParseFieldPaymentMethodType(string name, string fieldName, string value)
        {
            //0 - Fare is paid on board.
            //1 - Fare must be paid before boarding.

            switch (value)
            {
                case "0":
                    return PaymentMethodType.OnBoard;
                case "1":
                    return PaymentMethodType.BeforeBoarding;
            }
            throw new GTFSParseException(name, fieldName, value);
        }

        /// <summary>
        /// Parses an accessibility-type field.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private WheelchairAccessibilityType? ParseFieldAccessibilityType(string name, string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            { // there is no value.
                return null;
            }

            //0 (or empty) - indicates that there is no accessibility information for the trip
            //1 - indicates that the vehicle being used on this particular trip can accommodate at least one rider in a wheelchair
            //2 - indicates that no riders in wheelchairs can be accommodated on this trip

            switch (value)
            {
                case "0":
                    return WheelchairAccessibilityType.NoInformation;
                case "1":
                    return WheelchairAccessibilityType.SomeAccessibility;
                case "2":
                    return WheelchairAccessibilityType.NoAccessibility;
            }
            throw new GTFSParseException(name, fieldName, value);
        }

        /// <summary>
        /// Parses a drop-off-type field.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private DropOffType? ParseFieldDropOffType(string name, string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            { // there is no value.
                return null;
            }

            //0 - Regularly scheduled drop off
            //1 - No drop off available
            //2 - Must phone agency to arrange drop off
            //3 - Must coordinate with driver to arrange drop off

            switch (value)
            {
                case "0":
                    return DropOffType.Regular;
                case "1":
                    return DropOffType.NoPickup;
                case "2":
                    return DropOffType.PhoneForPickup;
                case "3":
                    return DropOffType.DriverForPickup;
            }
            throw new GTFSParseException(name, fieldName, value);
        }

        /// <summary>
        /// Parses a pickup-type field.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private PickupType? ParseFieldPickupType(string name, string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            { // there is no value.
                return null;
            }

            //0 - Regularly scheduled pickup
            //1 - No pickup available
            //2 - Must phone agency to arrange pickup
            //3 - Must coordinate with driver to arrange pickup

            switch (value)
            {
                case "0":
                    return PickupType.Regular;
                case "1":
                    return PickupType.NoPickup;
                case "2":
                    return PickupType.PhoneForPickup;
                case "3":
                    return PickupType.DriverForPickup;
            }
            throw new GTFSParseException(name, fieldName, value);
        }

        /// <summary>
        /// Parses a location-type field.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private LocationType? ParseFieldLocationType(string name, string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            { // there is no value.
                return null;
            }
            
            //0 or blank - Stop. A location where passengers board or disembark from a transit vehicle.
            //1 - Station. A physical structure or area that contains one or more stop.

            switch (value)
            {
                case "0":
                    return LocationType.Stop;
                case "1":
                    return LocationType.Station;
            }
            throw new GTFSParseException(name, fieldName, value);
        }

        /// <summary>
        /// Parses a direction-type field.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private DirectionType? ParseFieldDirectionType(string name, string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            { // there is no value.
                return null;
            }

            //0 - travel in one direction (e.g. outbound travel)
            //1 - travel in the opposite direction (e.g. inbound travel)

            switch (value)
            {
                case "0":
                    return DirectionType.OneDirection;
                case "1":
                    return DirectionType.OppositeDirection;
            }
            throw new GTFSParseException(name, fieldName, value);
        }

        /// <summary>
        /// Parses a positive integer field.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual uint? ParseFieldUInt(string name, string fieldName, string value)
        {
            if(string.IsNullOrWhiteSpace(value))
            { // there is no value.
                return null;
            }
            uint result;
            if(!uint.TryParse(value, out result))
            { // parsing failed!
                throw new GTFSParseException(name, fieldName, value);
            }
            return result;
        }

        /// <summary>
        /// Parses a double field.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected virtual double? ParseFieldDouble(string name, string fieldName, string value)
        {
            if(string.IsNullOrWhiteSpace(value))
            { // there is no value.
                return null;
            }

            double result;
            if (!double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result))
            { // parsing failed!
                throw new GTFSParseException(name, fieldName, value);
            }
            return result;
        }

        /// <summary>
        /// Parses a boolean field.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool? ParseFieldBool(string name, string fieldName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            { // there is no value.
                return null;
            }

            switch (value)
            {
                case "0":
                    return false;
                case "1":
                    return true;
            }
            throw new GTFSParseException(name, fieldName, value);
        }
    }
}